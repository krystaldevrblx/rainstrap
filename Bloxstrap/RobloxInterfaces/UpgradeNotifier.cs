using Bloxstrap.Models.Entities;
using System.Windows;
// MessageBoxResult lives in WinForms. The project sets UseWindowsForms, but that
// does not add the namespace globally, and IsAccept's whole point is to be a
// named, testable predicate over a dismissed-vs-declined dialog result - so it
// needs the type in scope.
using System.Windows.Forms;

namespace Bloxstrap.RobloxInterfaces
{
    /// <summary>
    /// What the update check concluded about the Roblox client.
    /// </summary>
    public enum UpdateAvailability
    {
        /// <summary>The check could not complete. Never acts as "no update".</summary>
        Unknown = 0,

        /// <summary>The selected version is the newest one published.</summary>
        UpToDate = 1,

        /// <summary>A newer official release exists.</summary>
        UpdateAvailable = 2,
    }

    /// <summary>
    /// What Notify should do about an available update.
    ///
    /// An enum rather than a bool so "do not prompt" has reasons that can be
    /// logged and asserted, and so no branch is reachable that silently mutates a
    /// selection.
    /// </summary>
    public enum NotifyDecision
    {
        /// <summary>No update, or Notify is off. Launch the selection unchanged.</summary>
        ProceedSilently = 0,

        /// <summary>An update exists; ask the user.</summary>
        PromptUser = 1,
    }

    /// <summary>
    /// The outcome of an update check against the selected version.
    /// </summary>
    public class UpdateDetectionResult
    {
        public UpdateAvailability Availability { get; set; } = UpdateAvailability.Unknown;

        public string SelectedVersionGuid { get; set; } = string.Empty;
        public string SelectedVersion { get; set; } = string.Empty;

        public string LatestVersionGuid { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;

        /// <summary>Why the check failed, when it did. Surfaced, never swallowed.</summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// True only when the latest release is genuinely newer, decided by
        /// comparing parsed version numbers.
        ///
        /// Never by comparing GUIDs: client version upload guids are opaque hashes,
        /// so their lexical order carries no meaning at all. The guids are only
        /// consulted to rule out the case where both spellings identify the very
        /// same version.
        /// </summary>
        public bool IsNewer
        {
            get
            {
                if (Availability != UpdateAvailability.UpdateAvailable)
                    return false;

                // When both guids are known and equal, the two spellings name the
                // same version and there is nothing to move to. Guids are only ever
                // used for this equality test - never for ordering.
                if (!String.IsNullOrEmpty(SelectedVersionGuid) && !String.IsNullOrEmpty(LatestVersionGuid))
                    return !String.Equals(LatestVersionGuid, SelectedVersionGuid, StringComparison.OrdinalIgnoreCase);

                // Without a guid to compare, the parsed version numbers have already
                // decided, and that verdict stands on its own.
                return true;
            }
        }
    }

    /// <summary>
    /// Update detection for the Roblox client, and the decision logic behind
    /// <see cref="UpgradeMode.Notify"/>.
    ///
    /// Detection and the prompt decision are separate pure functions so both can be
    /// exercised without a window or a network.
    /// </summary>
    public static class UpgradeNotifier
    {
        /// <summary>
        /// Compares a selected version against the channel's current release.
        ///
        /// A missing selected version is reported as <see cref="UpdateAvailability.Unknown"/>
        /// rather than "up to date": not knowing what the user is running is not the
        /// same as knowing it is current.
        /// </summary>
        public static UpdateDetectionResult Compare(string selectedVersion, string latestVersion)
        {
            const string LOG_IDENT = "UpgradeNotifier::Compare";

            var result = new UpdateDetectionResult
            {
                SelectedVersion = selectedVersion,
                LatestVersion = latestVersion,
            };

            if (String.IsNullOrWhiteSpace(selectedVersion))
            {
                App.Logger.WriteLine(LOG_IDENT, "No selected version to compare, treating the check as unknown");

                result.Availability = UpdateAvailability.Unknown;
                return result;
            }

            if (String.IsNullOrWhiteSpace(latestVersion))
            {
                App.Logger.WriteLine(LOG_IDENT, "No latest version available, treating the check as unknown");

                result.Availability = UpdateAvailability.Unknown;
                return result;
            }

            var selected = Utilities.ParseVersionSafe(selectedVersion);
            var latest = Utilities.ParseVersionSafe(latestVersion);

            if (selected is null || latest is null)
            {
                // Unparseable metadata means we cannot order the two honestly.
                // Claiming "up to date" here would hide a real update behind a
                // formatting problem.
                App.Logger.WriteLine(LOG_IDENT, $"Could not parse versions (selected='{selectedVersion}', latest='{latestVersion}'), treating the check as unknown");

                result.Availability = UpdateAvailability.Unknown;
                result.Error = "Version metadata could not be parsed";
                return result;
            }

            result.Availability = latest > selected
                ? UpdateAvailability.UpdateAvailable
                : UpdateAvailability.UpToDate;

            return result;
        }

        /// <summary>
        /// Whether to interrupt the user before launching.
        ///
        /// Every condition that should suppress the prompt is listed explicitly,
        /// including the channel check: an update for a channel the user is not on
        /// is not an update for them.
        /// </summary>
        public static NotifyDecision Decide(UpgradeMode mode, UpdateDetectionResult detection, bool selectionIsPinned, string? channel = null, bool alreadyPromptedThisLaunch = false)
        {
            const string LOG_IDENT = "UpgradeNotifier::Decide";

            if (mode != UpgradeMode.Notify)
                return NotifyDecision.ProceedSilently;

            // One prompt per launch attempt, no matter how many code paths ask.
            if (alreadyPromptedThisLaunch)
            {
                App.Logger.WriteLine(LOG_IDENT, "Already prompted during this launch, not prompting again");
                return NotifyDecision.ProceedSilently;
            }

            if (detection is null || !detection.IsNewer)
                return NotifyDecision.ProceedSilently;

            if (!String.IsNullOrEmpty(channel) &&
                !String.Equals(channel, Deployment.Channel, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Update is for channel '{channel}' but the user is on '{Deployment.Channel}'");
                return NotifyDecision.ProceedSilently;
            }

            // A pin is not by itself a reason to stay quiet. The whole point of
            // Notify is to tell the user a newer release exists and let them
            // decide, so this is recorded for the log and then overridden: the
            // prompt must appear, worded so it is obvious the pin will be kept
            // unless they choose otherwise.
            App.Logger.WriteLine(LOG_IDENT, selectionIsPinned
                ? "An update is available while a version is pinned; prompting so the user can keep their pin"
                : "An update is available; prompting");

            return NotifyDecision.PromptUser;
        }

        /// <summary>
        /// Whether a prompt answer means "go ahead and update".
        ///
        /// Exists as a named function because the dismissed case is easy to get
        /// wrong and impossible to spot by reading: closing the dialog yields
        /// <see cref="MessageBoxResult.None"/>, not <c>No</c>. Anything that is not
        /// an explicit Yes - No, Cancel, or a closed window - is a decline, which is
        /// what keeps a dismissed dialog from silently eating the user's choice.
        /// </summary>
        public static bool IsAccept(MessageBoxResult result) => result == MessageBoxResult.Yes;

        /// <summary>
        /// The message shown before launch.
        /// </summary>
        public static string BuildPrompt(UpdateDetectionResult detection, bool selectionIsPinned)
        {
            string selected = String.IsNullOrWhiteSpace(detection.SelectedVersion)
                ? detection.SelectedVersionGuid
                : detection.SelectedVersion;

            return String.Format(
                Strings.UpgradeNotify_AvailableMessage,
                selected,
                detection.LatestVersion,
                selectionIsPinned ? Strings.UpgradeNotify_PinnedNote : String.Empty).TrimEnd();
        }

        /// <summary>
        /// Reads the version number of the version that is currently selected.
        ///
        /// Prefers the recorded state, then the catalogue, then the binary on disk,
        /// so a comparison is never made against a blank string just because the
        /// version number was not cached at install time.
        /// </summary>
        public static string GetSelectedVersionNumber()
        {
            string guid = SelectedGuidOrInstalled();

            if (String.IsNullOrEmpty(guid))
                return String.Empty;

            var fromHistory = App.RobloxState.Prop.PlayerVersionHistory
                .FirstOrDefault(x => String.Equals(x.VersionGuid, guid, StringComparison.OrdinalIgnoreCase));

            if (fromHistory is not null && !String.IsNullOrWhiteSpace(fromHistory.Version))
                return fromHistory.Version;

            var validation = VersionControl.ValidateInstallation(guid);

            if (validation.DetectedVersion is not null)
                return validation.DetectedVersion;

            return String.Empty;
        }

        private static string SelectedGuidOrInstalled()
        {
            string selected = VersionControl.SelectedVersionGuid;

            if (!String.IsNullOrEmpty(selected))
                return selected;

            return Deployment.NormalizeVersionGuid(App.RobloxState.Prop.Player.VersionGuid);
        }
    }
}