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

        /// <summary>
        /// The player version the user pinned through Version Control.
        ///
        /// This is deliberately NOT <see cref="AppState.VersionGuid"/>. That one
        /// answers "what is installed right now", which changes on every ordinary
        /// update; this one answers "which version did the user ask to keep using",
        /// which must survive those updates untouched. Collapsing the two is what
        /// made a pinned version get silently replaced by the newest release.
        ///
        /// Empty means "follow the channel", i.e. normal Rainstrap behaviour.
        /// </summary>
        public string SelectedPlayerVersionGuid { get; set; } = string.Empty;

        /// <summary>
        /// True when <see cref="SelectedPlayerVersionGuid"/> was set by an explicit
        /// user action rather than by an accepted update prompt.
        ///
        /// This is the flag Notify consults before touching a selection: an
        /// explicit pin is never overwritten by a background discovery of a newer
        /// release. A prompt the user actually answered may still update it.
        /// </summary>
        public bool SelectedPlayerVersionIsPinned { get; set; } = false;

        /// <summary>
        /// Versions the user asked to keep on disk even though they are not the
        /// selected one. Cleanup must never delete these.
        /// </summary>
        public List<string> RetainedPlayerVersionGuids { get; set; } = new();

        /// <summary>
        /// Result of the most recent Version Control catalogue refresh, so reopening
        /// the page shows the last real answer instead of an empty list, and so a
        /// refresh can state plainly whether it was exhaustive.
        /// </summary>
        public VersionCatalogSnapshot? VersionCatalog { get; set; } = null;

        /// <summary>
        /// Last time an installed version was observed to be refused by Roblox,
        /// keyed by version guid.
        ///
        /// Recorded when a launch fails in a way that can be attributed to Roblox
        /// demanding an update. Purely informational: it changes what the UI
        /// advises, never what Rainstrap attempts. Bounded so it cannot grow
        /// without limit.
        /// </summary>
        public Dictionary<string, DateTime> RefusedVersionGuids { get; set; } = new();

        /// <summary>
        /// Prunes <see cref="RefusedVersionGuids"/> to the newest entries.
        /// </summary>
        public void TrimRefusedVersions()
        {
            const int maxEntries = 20;

            if (RefusedVersionGuids.Count <= maxEntries)
                return;

            RefusedVersionGuids = RefusedVersionGuids
                .OrderByDescending(x => x.Value)
                .Take(maxEntries)
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }

    /// <summary>
    /// A catalogue refresh, persisted so reopening the page shows the last real
    /// answer instead of an empty list.
    ///
    /// Flat and primitive-only for the same reason <see cref="State.LastUpdateCheckResult"/>
    /// is: it is deserialised straight out of RobloxState.json on every launch and
    /// must never be the thing that fails to load.
    /// </summary>
    public class VersionCatalogSnapshot
    {
        public string Channel { get; set; } = string.Empty;

        public DateTime RefreshedAtUtc { get; set; } = DateTime.MinValue;

        public bool IsExhaustive { get; set; } = false;

        public bool LatestResolved { get; set; } = false;

        public List<string> Limitations { get; set; } = new();

        public List<VersionCatalogSnapshotEntry> Entries { get; set; } = new();
    }

    public class VersionCatalogSnapshotEntry
    {
        public string VersionGuid { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public VersionAvailability Availability { get; set; } = VersionAvailability.Unknown;
        public VersionDiscoverySource Source { get; set; } = VersionDiscoverySource.ObservedLocally;
        public DateTime? PublishedUtc { get; set; } = null;
        public bool IsInstalled { get; set; } = false;
        public bool IsLatestOfficial { get; set; } = false;

        /// <summary>Rebuilds the runtime entry this snapshot was persisted from.</summary>
        public VersionCatalogEntry ToEntry() => new()
        {
            VersionGuid = VersionGuid,
            Version = Version,
            Channel = Channel,
            Availability = Availability,
            Source = Source,
            PublishedUtc = PublishedUtc,
            IsInstalled = IsInstalled,
            IsLatestOfficial = IsLatestOfficial,
        };

        public static VersionCatalogSnapshotEntry FromEntry(VersionCatalogEntry entry) => new()
        {
            VersionGuid = entry.VersionGuid,
            Version = entry.Version,
            Channel = entry.Channel,
            Availability = entry.Availability,
            Source = entry.Source,
            PublishedUtc = entry.PublishedUtc,
            IsInstalled = entry.IsInstalled,
            IsLatestOfficial = entry.IsLatestOfficial,
        };
    }
}
