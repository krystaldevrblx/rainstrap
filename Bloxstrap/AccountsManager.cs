using System.Security.Cryptography;

using Bloxstrap.Enums;
using Bloxstrap.Models;
using Bloxstrap.Models.APIs.Roblox;

namespace Bloxstrap
{
    /// <summary>
    /// Native Account Manager for Rainstrap.
    ///
    /// Account metadata is persisted to Accounts.json in the Rainstrap data folder.
    /// Authentication material (the Roblox security cookie that the locally installed
    /// Roblox client itself created) is captured from Roblox's own cookie storage and
    /// stored separately, encrypted per-user with DPAPI. It is never written to
    /// plaintext, never logged, and never displayed in the UI.
    /// </summary>
    public class AccountsManager : JsonManager<Accounts>
    {
        public override string ClassName => nameof(Accounts);

        public override string LOG_IDENT_CLASS => ClassName;

        public override string FileLocation => Path.Combine(Paths.Base, "Accounts.json");

        private string SecretsDirectory => Path.Combine(Paths.Base, "AccountSecrets");

        #region Persistence

        public void Load() => Load(false);

        private string GetSecretPath(string accountId) => Path.Combine(SecretsDirectory, $"{accountId}.bin");

        private void WriteSecret(string accountId, string token)
        {
            Directory.CreateDirectory(SecretsDirectory);

            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(GetSecretPath(accountId), encrypted);
        }

        private string? ReadSecret(string accountId)
        {
            string path = GetSecretPath(accountId);

            if (!File.Exists(path))
                return null;

            byte[] decrypted = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        private void DeleteSecret(string accountId)
        {
            string path = GetSecretPath(accountId);

            if (File.Exists(path))
                File.Delete(path);
        }

        #endregion

        #region Queries

        public SavedAccount? GetAccount(string id) => Prop.Items.FirstOrDefault(x => x.Id == id);

        public SavedAccount? GetActiveAccount()
        {
            if (String.IsNullOrEmpty(Prop.ActiveAccountId))
                return null;

            return GetAccount(Prop.ActiveAccountId);
        }

        public bool IsKnownUserId(long userId) => Prop.Items.Any(x => x.UserId == userId);

        /// <summary>
        /// Whether stored sign-in material exists for this account. Never
        /// exposes the secret itself - only its presence, so the UI can flag
        /// accounts whose captured session is missing (e.g. deleted manually).
        /// </summary>
        public bool HasSecret(string id) => File.Exists(GetSecretPath(id));

        #endregion

        #region Capture / add

        /// <summary>
        /// Reads the currently logged-in account from the local Roblox installation
        /// (via CookiesManager) and returns its metadata. The security token is not
        /// included in the result - it is only persisted by <see cref="AddCurrentAccountAsync"/>.
        /// </summary>
        public async Task<SavedAccount?> CaptureCurrentAccountAsync()
        {
            const string LOG_IDENT = "AccountsManager::CaptureCurrentAccountAsync";

            if (!App.Settings.Prop.AllowCookieAccess)
                throw new InvalidOperationException("Cookie access is not enabled");

            await App.Cookies.LoadCookies();

            if (App.Cookies.State != CookieState.Success)
                throw new InvalidOperationException($"Could not read the current Roblox session ({App.Cookies.State})");

            AuthenticatedUser? user = await App.Cookies.GetAuthenticated();

            if (user is null || user.Id == 0)
                throw new InvalidOperationException("Roblox did not report a logged in user for the current session");

            App.Logger.WriteLine(LOG_IDENT, $"Captured current session for user {user.Id}");

            var account = new SavedAccount
            {
                UserId = user.Id,
                Username = user.Username,
                AccountDisplayName = user.Displayname,
                AvatarUrl = await GetAvatarUrlAsync(user.Id)
            };

            SavedAccount? existing = Prop.Items.FirstOrDefault(x => x.UserId == user.Id);
            if (existing is not null)
            {
                existing.Username = account.Username;
                existing.AccountDisplayName = account.AccountDisplayName;
                existing.AvatarUrl = account.AvatarUrl;
                return existing;
            }

            return account;
        }

        /// <summary>
        /// Captures the current Roblox session and saves it as an account.
        /// If the Roblox user was already saved, its metadata and secret are refreshed instead.
        /// </summary>
        public async Task<(SavedAccount Account, bool AlreadyExisted)> AddCurrentAccountAsync(string? displayName = null)
        {
            const string LOG_IDENT = "AccountsManager::AddCurrentAccountAsync";

            SavedAccount? account = await CaptureCurrentAccountAsync();

            if (account is null)
                throw new InvalidOperationException("No active Roblox session was found");

            // refresh the stored secret for this Roblox user
            WriteSecret(account.Id, App.Cookies.AuthCookieValue);

            bool alreadyExisted = Prop.Items.Any(x => x.Id == account.Id);

            if (!alreadyExisted)
                Prop.Items.Add(account);

            if (!String.IsNullOrWhiteSpace(displayName))
                account.AccountDisplayName = displayName.Trim();

            Save();

            App.Logger.WriteLine(LOG_IDENT, $"Saved account '{account.Username}' ({account.UserId})");

            return (account, alreadyExisted);
        }

        private static async Task<string> GetAvatarUrlAsync(long userId)
        {
            const string LOG_IDENT = "AccountsManager::GetAvatarUrlAsync";

            try
            {
                Uri uri = new($"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false");
                HttpResponseMessage response = await App.HttpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                JsonElement first = document.RootElement.GetProperty("data")[0];

                if (first.TryGetProperty("imageUrl", out JsonElement url))
                    return url.GetString() ?? "";
            }
            catch (Exception ex)
            {
                // avatar is optional metadata; never fail account capture because of it
                App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch avatar for user {userId}: {ex.Message}");
            }

            return "";
        }

        #endregion

        #region Management

        public void RenameAccount(string id, string displayName)
        {
            const string LOG_IDENT = "AccountsManager::RenameAccount";

            SavedAccount? account = GetAccount(id);

            if (account is null)
                return;

            account.AccountDisplayName = displayName.Trim();
            Save();

            App.Logger.WriteLine(LOG_IDENT, $"Renamed account {account.UserId}");
        }

        public void RemoveAccount(string id)
        {
            const string LOG_IDENT = "AccountsManager::RemoveAccount";

            SavedAccount? account = GetAccount(id);

            if (account is null)
                return;

            Prop.Items.Remove(account);
            DeleteSecret(id);

            if (Prop.ActiveAccountId == id)
                Prop.ActiveAccountId = null;

            Save();

            App.Logger.WriteLine(LOG_IDENT, $"Removed saved account {account.UserId}");
        }

        /// <summary>
        /// Marks an account as active. It will be applied on the next Roblox launch.
        /// Pass null to fall back to whatever account is logged into the local Roblox install.
        /// </summary>
        public void SetActiveAccount(string? id)
        {
            if (id is not null && GetAccount(id) is null)
                return;

            Prop.ActiveAccountId = id;

            foreach (SavedAccount item in Prop.Items)
            {
                if (item.Id == id)
                    item.LastUsedAt = DateTime.UtcNow;
            }

            Save();
        }

        #endregion

        #region Launch integration

        /// <summary>
        /// Writes the active account's authentication material into the local Roblox
        /// client's own cookie storage, so that the next launched client signs into it.
        /// Returns false (and logs) if it could not be applied - launching proceeds
        /// with whatever account the Roblox client is currently signed into.
        /// </summary>
        public bool ApplyActiveCookieForLaunch()
        {
            const string LOG_IDENT = "AccountsManager::ApplyActiveCookieForLaunch";

            SavedAccount? account = GetActiveAccount();

            if (account is null)
                return true; // no account selected - nothing to do

            try
            {
                string? token = ReadSecret(account.Id);

                if (String.IsNullOrEmpty(token))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Stored credentials for '{account.Username}' are missing");
                    return false;
                }

                bool result = App.Cookies.WriteAuthCookie(token!);

                App.Logger.WriteLine(LOG_IDENT, result
                    ? $"Applied account '{account.Username}' for next launch"
                    : $"Failed to apply account '{account.Username}' for next launch");

                return result;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to apply account '{account.Username}': {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
