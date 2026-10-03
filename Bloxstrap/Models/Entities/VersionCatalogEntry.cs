namespace Bloxstrap.Models.Entities
{
    /// <summary>
    /// One Roblox version in the Version Control catalogue.
    ///
    /// Purely a data record: it carries what was discovered and never decides
    /// anything itself. Availability is stored as a tri-state so "not checked"
    /// survives a round-trip through disk instead of silently becoming "unavailable".
    /// </summary>
    public class VersionCatalogEntry
    {
        /// <summary>Canonical client version upload guid, always "version-" prefixed.</summary>
        public string VersionGuid { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable version, e.g. "0.741.0.7411058". Empty when Roblox did
        /// not tell us and no local binary could supply it.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>Channel this version came from, when known.</summary>
        public string Channel { get; set; } = string.Empty;

        public VersionAvailability Availability { get; set; } = VersionAvailability.Unknown;

        public VersionDiscoverySource Source { get; set; } = VersionDiscoverySource.ObservedLocally;

        public DateTime? PublishedUtc { get; set; } = null;

        /// <summary>True when a usable installation of this version is on disk.</summary>
        public bool IsInstalled { get; set; } = false;

        /// <summary>True when this is the version the user pinned in Version Control.</summary>
        public bool IsSelected { get; set; } = false;

        /// <summary>True only for the version the channel currently publishes.</summary>
        public bool IsLatestOfficial { get; set; } = false;

        /// <summary>True when Roblox published this version but files are not local.</summary>
        public bool CanDownload => Availability == VersionAvailability.Available && !IsInstalled;

        /// <summary>
        /// Version string if it parses as one, otherwise null.
        ///
        /// Used for ordering. Roblox version strings are four-part dotted numbers
        /// and comparing them as text sorts 0.9.0.0 above 0.10.0.0, so anything
        /// unparseable sorts last rather than being guessed at.
        /// </summary>
        public Version? ParsedVersion =>
            String.IsNullOrWhiteSpace(Version) ? null : Utilities.ParseVersionSafe(Version);
    }

    /// <summary>
    /// The outcome of one catalogue refresh.
    ///
    /// Carries <see cref="IsExhaustive"/> and <see cref="Limitations"/> because the
    /// honest answer from Roblox's public APIs is always "this is not every version
    /// that has ever existed". Presenting the list without that qualifier would be
    /// claiming a completeness Roblox does not offer.
    /// </summary>
    public class VersionCatalogResult
    {
        public List<VersionCatalogEntry> Entries { get; set; } = new();

        /// <summary>
        /// Binary types whose current deployment could not be read this refresh.
        /// Non-empty means the list is missing at least the newest release.
        /// </summary>
        public List<string> Failures { get; set; } = new();

        /// <summary>False when the current release could not be determined.</summary>
        public bool LatestResolved { get; set; } = false;

        /// <summary>
        /// Always false in practice. Roblox publishes no endpoint that enumerates
        /// historical versions, so a complete list cannot be produced.
        /// </summary>
        public bool IsExhaustive { get; set; }

        public DateTime RefreshedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Human-readable reasons the list is partial. Surfaced verbatim so the UI
        /// states the limitation instead of implying completeness.
        /// </summary>
        public List<string> Limitations { get; set; } = new();

        /// <summary>
        /// Orders newest first using parsed version metadata.
        ///
        /// Deliberately not a string sort and deliberately not a GUID sort: version
        /// guids are opaque hashes with no ordering, so ordering by them would be
        /// arbitrary. Entries whose version cannot be parsed fall to the end, and
        /// ties fall back to install/selection state so the useful ones surface.
        /// </summary>
        public List<VersionCatalogEntry> SortedEntries()
        {
            return Entries
                .OrderByDescending(x => x.ParsedVersion is not null)
                .ThenByDescending(x => x.ParsedVersion)
                .ThenByDescending(x => x.PublishedUtc ?? DateTime.MinValue)
                .ThenByDescending(x => x.IsSelected)
                .ThenByDescending(x => x.IsInstalled)
                .ToList();
        }
    }

    /// <summary>
    /// What a probe of one version established.
    /// </summary>
    public readonly struct VersionProbeResult
    {
        public VersionAvailability State { get; }

        public DateTime? LastModifiedUtc { get; }

        public VersionProbeResult(VersionAvailability state, DateTime? lastModifiedUtc)
        {
            State = state;
            LastModifiedUtc = lastModifiedUtc;
        }
    }

    /// <summary>
    /// Why a local installation was rejected.
    /// </summary>
    public enum InstallationValidationResult
    {
        /// <summary>Executable present and readable.</summary>
        Valid = 0,

        /// <summary>No install folder for this version.</summary>
        Missing = 1,

        /// <summary>Folder exists but the executable does not.</summary>
        ExecutableMissing = 2,

        /// <summary>Executable exists but is empty or implausibly small.</summary>
        ExecutableTruncated = 3,

        /// <summary>Executable exists but reports no usable version.</summary>
        VersionUnreadable = 4,
    }

    /// <summary>
    /// Result of validating an installed version before it is offered for launch.
    /// </summary>
    public readonly struct InstallationValidation
    {
        public InstallationValidationResult Result { get; }

        public string? DetectedVersion { get; }

        public long SizeBytes { get; }

        public InstallationValidation(InstallationValidationResult result, string? detectedVersion, long sizeBytes)
        {
            Result = result;
            DetectedVersion = detectedVersion;
            SizeBytes = sizeBytes;
        }

        public bool IsValid => Result == InstallationValidationResult.Valid;

        public bool RequiresReinstall =>
            Result is InstallationValidationResult.Missing
                  or InstallationValidationResult.ExecutableMissing
                  or InstallationValidationResult.ExecutableTruncated;
    }
}