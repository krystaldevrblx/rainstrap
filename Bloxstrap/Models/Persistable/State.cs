using System.Windows.Forms;

namespace Bloxstrap.Models.Persistable
{
    public class State
    {
        public bool TestModeWarningShown { get; set; } = false;

        public bool IgnoreOutdatedChannel { get; set; } = false;

        public bool WatcherRunning { get; set; } = false;

        public bool PromptWebView2Install { get; set; } = true;

        public string? LastPage {  get; set; } = null!;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        /// <summary>
        /// Roblox instances that Rainstrap has launched, with the account they were launched for (if known).
        /// </summary>
        public List<InstanceEntry> Instances { get; set; } = new();

        /// <summary>
        /// UTC timestamp of the last manual Roblox update check from the Updates page.
        /// </summary>
        public DateTime? LastUpdateCheckUtc { get; set; } = null;


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
