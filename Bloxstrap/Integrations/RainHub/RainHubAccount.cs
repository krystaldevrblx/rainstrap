using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Bloxstrap.Integrations.RainHub
{
    /// <summary>
    /// Persisted RainHub link state for this Rainstrap installation.
    ///
    /// Only non-secret metadata is stored in plaintext. The device credential itself is
    /// held as a DPAPI-encrypted blob scoped to the current Windows user, so it cannot be
    /// read by another account on the machine or lifted from a file backup.
    ///
    /// The RainHub password is never seen, requested or stored by Rainstrap. Linking uses
    /// RainHub's existing device pairing flow: the signed-in user generates a short code
    /// in their RainHub dashboard, and Rainstrap exchanges that code for a scoped device
    /// credential.
    /// </summary>
    public class RainHubAccount : JsonManager<RainHubAccount.State>
    {
        public override string ClassName => nameof(RainHubAccount);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string FileLocation => Path.Combine(Paths.Base, "RainHubAccount.json");

        public override void Load(bool alertFailure = false)
        {
            base.Load(alertFailure);

            // Never surface a credential through the generic JsonManager failure dialog.
            Prop = Prop ?? new State();
        }

        public class State
        {
            /// <summary>RainHub's public device id for this installation (e.g. dev_...).</summary>
            public string? DeviceId { get; set; }

            /// <summary>DPAPI (CurrentUser) encrypted device credential. Base64.</summary>
            public string? EncryptedToken { get; set; }

            public DateTimeOffset? LinkedAt { get; set; }

            public DateTimeOffset? LastHeartbeatAt { get; set; }

            /// <summary>Roblox channel reported at the last successful heartbeat.</summary>
            public string? LastKnownChannel { get; set; }
        }

        #region Token access

        private static string? _cachedToken;
        private static bool _tokenResolved;

        /// <summary>
        /// The scoped device credential, decrypted on demand. Returns null when this
        /// installation is not linked. Never logged.
        /// </summary>
        public static string? DeviceToken
        {
            get
            {
                if (_tokenResolved)
                    return _cachedToken;

                _tokenResolved = true;
                _cachedToken = null;

                try
                {
                    string? encrypted = App.RainHubAccount.Prop.EncryptedToken;

                    if (string.IsNullOrEmpty(encrypted))
                        return null;

                    byte[] blob = Convert.FromBase64String(encrypted);
                    byte[] plain = ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser);

                    _cachedToken = Encoding.UTF8.GetString(plain);
                }
                catch (Exception ex)
                {
                    // Most likely the blob was written by a different Windows user or the
                    // profile was reset. Treat the link as broken rather than crashing.
                    App.Logger.WriteLine("RainHubAccount", "Stored device credential could not be decrypted");
                    App.Logger.WriteException("RainHubAccount", ex);
                }

                return _cachedToken;
            }
        }

        /// <summary>
        /// DPAPI optional entropy. Fixed so the blob stays readable across runs; the
        /// protection that matters is the CurrentUser scope, not this value.
        /// </summary>
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Rainstrap.RainHub.DeviceToken.v1");

        private static string Protect(string token)
        {
            byte[] plain = Encoding.UTF8.GetBytes(token);
            byte[] blob = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(blob);
        }

        #endregion

        #region State

        public static bool IsLinked => !string.IsNullOrEmpty(DeviceToken);

        public static string? DeviceId => App.RainHubAccount.Prop.DeviceId;

        public static DateTimeOffset? LinkedAt => App.RainHubAccount.Prop.LinkedAt;

        public static void ResetCache()
        {
            _cachedToken = null;
            _tokenResolved = false;
        }

        #endregion

        #region Operations

        /// <summary>
        /// Exchanges a user-generated pairing code for a device credential and stores it
        /// encrypted. Mirrors RainHub's own device-side pairing endpoint exactly.
        /// </summary>
        public async Task<RainHubResult<RainHubAccount>> LinkAsync(
            string code, string deviceName, CancellationToken cancellationToken = default)
        {
            string trimmed = (code ?? "").Trim().ToUpperInvariant();

            if (trimmed.Length != 6 || !trimmed.All(char.IsLetterOrDigit))
            {
                return RainHubResult<RainHubAccount>.Fail(
                    RainHubError.RequestRejected, "Enter the 6 character code from RainHub.");
            }

            var result = await RainHubClient.PairAsync(trimmed, deviceName, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success || result.Value is null)
            {
                return RainHubResult<RainHubAccount>.Fail(
                    result.Error,
                    result.Error == RainHubError.RequestRejected
                        ? "That code was not accepted. Codes expire after 15 minutes and can only be used once."
                        : result.Message);
            }

            Prop.DeviceId = result.Value.DeviceId;
            Prop.EncryptedToken = Protect(result.Value.DeviceToken);
            Prop.LinkedAt = DateTimeOffset.UtcNow;
            Prop.LastHeartbeatAt = null;
            Prop.LastKnownChannel = App.Settings.Prop.Channel;
            Save();

            ResetCache();

            App.Logger.WriteLine(
                "RainHubAccount", $"Linked Rainstrap to RainHub device {Prop.DeviceId}");

            return RainHubResult<RainHubAccount>.Ok(this);
        }

        /// <summary>
        /// Removes the local link. The RainHub device itself stays visible in the user's
        /// RainHub dashboard, where it can be revoked - deliberately, so a lost token can
        /// always be closed out from the authoritative side.
        /// </summary>
        public void Unlink()
        {
            Prop.DeviceId = null;
            Prop.EncryptedToken = null;
            Prop.LinkedAt = null;
            Prop.LastHeartbeatAt = null;
            Prop.LastKnownChannel = null;
            Save();

            ResetCache();

            App.Logger.WriteLine("RainHubAccount", "RainHub link removed from this installation");
        }

        /// <summary>
        /// Reports liveness to RainHub. Safe to call when not linked (it just does
        /// nothing), so callers do not need to guard.
        /// </summary>
        public async Task<bool> HeartbeatAsync(bool robloxRunning, CancellationToken cancellationToken = default)
        {
            if (!IsLinked)
                return false;

            var result = await RainHubClient.HeartbeatAsync(robloxRunning, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Prop.LastHeartbeatAt = DateTimeOffset.UtcNow;
                Prop.LastKnownChannel = App.Settings.Prop.Channel;
                Save();
                return true;
            }

            if (result.Error == RainHubError.NotLinked || result.Error == RainHubError.Forbidden)
            {
                // RainHub no longer accepts this credential; drop it so the UI reflects
                // reality instead of repeatedly failing.
                App.Logger.WriteLine(
                    "RainHubAccount", "Device credential rejected by RainHub - clearing local link");
                Unlink();
            }

            return false;
        }

        #endregion
    }
}
