namespace Bloxstrap.Models.Persistable
{
    /// <summary>
    /// A previously installed Roblox player version, recorded locally when
    /// Rainstrap finishes installing/upgrading the client. Used by the Updates
    /// page to show a version-control-style history and to offer explicit
    /// rollback to versions that are still downloadable.
    /// </summary>
    public class PlayerVersionHistoryEntry
    {
        public string VersionGuid { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable version (e.g. "0.700.123"), when it was known at
        /// install time. May be empty for installs launched with an explicit
        /// version argument where the deploy API was not consulted.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>The Roblox channel this install came from.</summary>
        public string Channel { get; set; } = "production";

        public DateTime InstalledAtUtc { get; set; }
    }
}
