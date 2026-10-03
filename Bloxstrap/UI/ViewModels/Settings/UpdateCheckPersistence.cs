// Logic that must be extractable to be testable. Kept separate from
// UpdatesViewModel so the persistence rules can be tested without a WPF window,
// a live Roblox connection or a DPAPI-protected file on disk.
using System;
using System.Text.Json;

using Bloxstrap.Integrations.RainHub;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// Whether a saved check can be shown, and if not, why.
    /// </summary>
    public enum RestoredCheckOutcome
    {
        /// <summary>Nothing usable was saved.</summary>
        None,

        /// <summary>Saved, but for a different channel than the one in use.</summary>
        WrongChannel,

        /// <summary>Saved and usable.</summary>
        Restored,
    }

    /// <summary>
    /// The saved-update-check rules, as pure functions over the persisted record.
    ///
    /// These are the decisions that decide whether the Updates tab shows a real
    /// answer after a restart or quietly says "not checked", so they are separated
    /// from the view model that consumes them and tested directly.
    /// </summary>
    public static class UpdateCheckPersistence
    {
        /// <summary>Channels match case-insensitively; Roblox channel names differ in case.</summary>
        public static bool ChannelMatches(string? saved, string current)
        {
            if (string.IsNullOrEmpty(saved))
                return true; // No channel recorded: nothing to contradict.
            return string.Equals(saved, current, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a saved record carries everything needed to display it.
        /// A record without a version is not a result, however new it is.
        /// </summary>
        public static bool IsUsable(Bloxstrap.Models.Persistable.State.LastUpdateCheckResult? saved)
        {
            if (saved is null)
                return false;
            if (string.IsNullOrWhiteSpace(saved.Version))
                return false;
            return saved.CheckedUtc != DateTime.MinValue;
        }

        /// <summary>
        /// The full decision, and the record itself when there is one to show.
        ///
        /// Returning the record rather than only an enum keeps the null check in one
        /// place: a caller that gets a non-null <see cref="Restored"/> is holding a
        /// record already known to carry a version, so it does not have to re-derive
        /// or re-assert that.
        /// </summary>
        public static (RestoredCheckOutcome Outcome, Bloxstrap.Models.Persistable.State.LastUpdateCheckResult? Restored)
            Evaluate(
            Bloxstrap.Models.Persistable.State.LastUpdateCheckResult? saved,
            string currentChannel)
        {
            if (saved is null)
                return (RestoredCheckOutcome.None, null);

            if (string.IsNullOrWhiteSpace(saved.Version) || saved.CheckedUtc == DateTime.MinValue)
                return (RestoredCheckOutcome.None, null);

            if (!ChannelMatches(saved.Channel, currentChannel))
                return (RestoredCheckOutcome.WrongChannel, null);

            return (RestoredCheckOutcome.Restored, saved);
        }

        /// <summary>
        /// Whether a completed check should be persisted.
        ///
        /// A check that threw has no answer to save, and writing an empty record
        /// would replace a good previous result with nothing - which is exactly the
        /// failure this whole mechanism exists to prevent.
        /// </summary>
        public static bool ShouldPersist(bool succeeded, string? version)
        {
            return succeeded && !string.IsNullOrWhiteSpace(version);
        }

        /// <summary>
        /// Builds the record to persist from a successful check.
        /// </summary>
        public static Bloxstrap.Models.Persistable.State.LastUpdateCheckResult Build(
            string channel,
            string version,
            string? versionGuid,
            DateTime? publishedUtc,
            DateTime checkedUtc,
            long latencyMs)
            => new()
            {
                Channel = channel,
                Version = version,
                VersionGuid = versionGuid,
                PublishedUtc = publishedUtc,
                CheckedUtc = checkedUtc,
                LatencyMs = latencyMs,
            };
    }

    /// <summary>
    /// The version-history rules: which entries are shown, and how they are
    /// labelled relative to the current and latest versions.
    /// </summary>
    public static class VersionHistoryRules
    {
        public enum VersionState
        {
            Current,
            Latest,
            Previous,
        }

        /// <summary>
        /// How one history entry should be badged. Current wins over latest, so a
        /// game that is both installed and newest is not labelled twice.
        /// </summary>
        public static VersionState Classify(
            string entryVersionGuid,
            string? currentVersionGuid,
            string? latestVersionGuid)
        {
            if (!string.IsNullOrEmpty(currentVersionGuid)
                && string.Equals(entryVersionGuid, currentVersionGuid, StringComparison.Ordinal))
                return VersionState.Current;

            if (!string.IsNullOrEmpty(latestVersionGuid)
                && string.Equals(entryVersionGuid, latestVersionGuid, StringComparison.Ordinal))
                return VersionState.Latest;

            return VersionState.Previous;
        }

        /// <summary>
        /// Whether a history entry is one Version Control can act on.
        ///
        /// The installed version and the latest published version are not
        /// candidates: the first is already running, the second is what launching
        /// would install anyway.
        /// </summary>
        public static bool IsSelectableInHistory(
            string entryVersionGuid,
            string? currentVersionGuid,
            string? latestVersionGuid)
            => Classify(entryVersionGuid, currentVersionGuid, latestVersionGuid) == VersionState.Previous;
    }

    /// <summary>
    /// RainHub account-linking state, as a pure decision over the credential's
    /// presence. Kept apart from the DPAPI-backed account store so the rules can
    /// be tested without a device credential or a logged file.
    /// </summary>
    public static class RainHubLinkState
    {
        public enum LinkState
        {
            /// <summary>No usable credential: the tab is hidden and settings offers linking.</summary>
            Unlinked,

            /// <summary>RainHub rejected the credential: the tab is hidden and relinking is offered.</summary>
            NeedsRelink,

            /// <summary>Usable credential: the tab is shown.</summary>
            Linked,
        }

        /// <summary>
        /// Whether a link is usable. A token that failed to decrypt - typically
        /// because the blob belongs to a different Windows user - is not a link,
        /// and is treated the same as never having linked.
        /// </summary>
        public static bool HasUsableToken(string? token)
            => !string.IsNullOrEmpty(token);

        /// <summary>
        /// The visible state. <paramref name="rejectedByServer"/> distinguishes
        /// "we hold no credential" from "RainHub stopped accepting ours", which
        /// need different copy and different next actions.
        /// </summary>
        public static LinkState Evaluate(string? token, bool rejectedByServer)
        {
            if (!HasUsableToken(token))
                return rejectedByServer ? LinkState.NeedsRelink : LinkState.Unlinked;

            return LinkState.Linked;
        }

        /// <summary>
        /// Whether a RainHub error means the stored credential should be discarded.
        ///
        /// Only "we were told to authenticate" and "we are not allowed" mean the
        /// credential itself is the problem. A rate limit or an outage must not
        /// unlink a working device.
        /// </summary>
        public static bool ShouldUnlink(RainHubError error)
            => error is RainHubError.NotLinked or RainHubError.Forbidden;
    }
}