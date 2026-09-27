namespace Bloxstrap.Models.Persistable
{
    public class RobloxState
    {
        /// <summary>Maximum number of recorded player versions to keep.</summary>
        public const int VersionHistoryMaxEntries = 10;

        public AppState Player { get; set; } = new();

        public AppState Studio { get; set; } = new();

        public List<string> ModManifest { get; set; } = new();

        /// <summary>
        /// Locally recorded history of installed player versions (most recent
        /// last). Bounded by <see cref="VersionHistoryMaxEntries"/>.
        /// </summary>
        public List<PlayerVersionHistoryEntry> PlayerVersionHistory { get; set; } = new();
    }
}
