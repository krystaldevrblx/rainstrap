namespace Bloxstrap.Enums
{
    /// <summary>
    /// Whether a version can actually be downloaded from Roblox right now.
    ///
    /// Deliberately three-state rather than a bool. "We have not checked yet" and
    /// "we checked and Roblox withdrew it" are very different answers to give a
    /// user, and collapsing them into a bool is how a version ends up presented as
    /// available purely because a GUID happens to exist in local history.
    /// </summary>
    public enum VersionAvailability
    {
        /// <summary>Not probed yet, or the probe failed for a transport reason.</summary>
        Unknown = 0,

        /// <summary>
        /// Roblox served this version's package manifest, so the files are still
        /// published and can be downloaded.
        /// </summary>
        Available = 1,

        /// <summary>
        /// Roblox answered the manifest request with a not-found/forbidden status,
        /// meaning this version is no longer published and cannot be downloaded.
        /// </summary>
        Unavailable = 2,
    }
}