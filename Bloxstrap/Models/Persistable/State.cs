using System.Windows.Forms;

namespace Bloxstrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool WatcherRunning { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage { get; set; } = null!;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        /// <summary>
        /// UTC timestamp of the last manual Roblox update check from the Updates page.
        /// </summary>
        public DateTime? LastUpdateCheckUtc { get; set; } = null;

        /// <summary>
        /// The result of the last SUCCESSFUL update check, so the Updates tab can show
        /// a real answer after a restart instead of "not checked".
        ///
        /// Only written on success. A failed check must leave the previous good result
        /// intact, otherwise a transient network error would destroy the only thing the
        /// user actually learned, and the tab would silently forget a real update.
        /// </summary>
        public LastUpdateCheckResult? LastUpdateCheck { get; set; } = null;

        /// <summary>
        /// A completed update check, as persisted between sessions.
        ///
        /// Deliberately a flat, plain shape: it is deserialised straight out of
        /// State.json on every launch, so it must not reference anything that could
        /// fail to deserialise. A missing or unparseable record is treated as "no
        /// result yet" rather than as an error, so an older State.json still loads.
        /// </summary>
        public class LastUpdateCheckResult
        {
            /// <summary>Channel the check was made against, e.g. "production".</summary>
            public string? Channel { get; set; } = null;

            /// <summary>Version string the channel reported, e.g. "0.657.0.6570".</summary>
            public string? Version { get; set; } = null;

            /// <summary>Client version upload guid of that release.</summary>
            public string? VersionGuid { get; set; } = null;

            /// <summary>When Roblox published it, if the header was present.</summary>
            public DateTime? PublishedUtc { get; set; } = null;

            /// <summary>When we performed the check.</summary>
            public DateTime CheckedUtc { get; set; } = DateTime.MinValue;

            /// <summary>
            /// How long the request took. Kept so the tab can show the same latency
            /// figure after a restart rather than resetting it to "not measured".
            /// </summary>
            public long LatencyMs { get; set; } = -1;

            /// <summary>
            /// The result is only meaningful for the channel it was made against. Kept
            /// so a stale record from another channel is never presented as if it
            /// described the current one.
            /// </summary>
            public bool IsValid => !string.IsNullOrEmpty(Version);
        }

        #region Deprecated properties
        /// <summary>
        /// Deprecated, use App.RobloxState.Player
        /// </summary>
        public AppState? Player { private get; set; }
        public AppState? GetDeprecatedPlayer() => Player;

        /// <summary>
        /// Deprecated, use App.RobloxState.Studio
        /// </summary>
        public AppState? Studio { private get; set; }
        public AppState? GetDeprecatedStudio() => Studio;

        /// <summary>
        /// Deprecated, use App.RobloxState.ModManifest
        /// </summary>
        public List<string>? ModManifest { private get; set; }
        public List<string>? GetDeprecatedModManifest() => ModManifest;
        #endregion
    }
}
