namespace Bloxstrap.Enums
{
    /// <summary>
    /// How usable a version actually is for launching.
    ///
    /// This is deliberately separate from <see cref="VersionAvailability"/>, which
    /// only answers "can the files still be downloaded". A version can be perfectly
    /// downloadable and still be refused by Roblox at runtime, and saying
    /// "supported" on the strength of files existing on disk would be a lie.
    /// </summary>
    public enum VersionSupportState
    {
        /// <summary>Not evaluated yet.</summary>
        Unknown = 0,

        /// <summary>
        /// Files are present locally and pass validation, and the version is still
        /// published. This is the only state Version Control will offer to select.
        /// </summary>
        Supported = 1,

        /// <summary>
        /// Installed locally but the installation does not validate - missing or
        /// truncated files. Recoverable by reinstalling.
        /// </summary>
        CorruptedInstallation = 2,

        /// <summary>
        /// Roblox no longer publishes this version, so it cannot be downloaded.
        /// An existing local copy may still run, but it cannot be repaired.
        /// </summary>
        NoLongerPublished = 3,

        /// <summary>
        /// Files are valid but Roblox rejected the launch, typically demanding an
        /// update. Rainstrap cannot fix this and must not try to.
        /// </summary>
        BlockedByRoblox = 4,
    }
}