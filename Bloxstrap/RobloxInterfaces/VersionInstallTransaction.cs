namespace Bloxstrap.RobloxInterfaces
{
    /// <summary>
    /// The file-level part of a version install: move the existing copy aside,
    /// then either discard it or put it back.
    ///
    /// Extracted from the bootstrapper so the guarantee it exists to provide -
    /// "a failed install never costs the user their working version" - can be
    /// exercised directly against a temporary directory, rather than only being
    /// asserted by reading the bootstrapper.
    ///
    /// Deliberately static and path-based: no client state, no network, no UI.
    /// </summary>
    public static class VersionInstallTransaction
    {
        /// <summary>Suffix appended to a version folder while its replacement installs.</summary>
        public const string StagedSuffix = ".previous";

        /// <summary>
        /// Moves the live folder aside so it can be restored.
        /// </summary>
        /// <returns>The staged path, or null when there was nothing to stage.</returns>
        public static string? Stage(string liveDirectory)
        {
            if (!Directory.Exists(liveDirectory))
                return null;

            string staged = liveDirectory + StagedSuffix;

            // A leftover staging folder means a previous run died mid-install.
            // It has already been superseded by whatever is live now, so it is
            // safe to clear rather than accumulate.
            if (Directory.Exists(staged))
                Directory.Delete(staged, true);

            Directory.Move(liveDirectory, staged);

            return staged;
        }

        /// <summary>
        /// Discards the staged copy. Only call this once the replacement has been
        /// validated - it is the point of no return.
        /// </summary>
        public static bool Commit(string? stagedDirectory)
        {
            if (stagedDirectory is null || !Directory.Exists(stagedDirectory))
                return false;

            Directory.Delete(stagedDirectory, true);

            return true;
        }

        /// <summary>
        /// Removes the failed replacement and restores the staged copy.
        ///
        /// Returns true when a working version was put back. A false return means
        /// the user is left with nothing installed, which the caller must treat as
        /// a hard failure rather than something to continue past.
        /// </summary>
        public static bool Rollback(string liveDirectory, string? stagedDirectory)
        {
            if (stagedDirectory is null || !Directory.Exists(stagedDirectory))
                return false;

            if (Directory.Exists(liveDirectory))
                Directory.Delete(liveDirectory, true);

            Directory.Move(stagedDirectory, liveDirectory);

            return Directory.Exists(liveDirectory);
        }

        /// <summary>
        /// Whether staging would be pointless.
        ///
        /// A fresh install has no folder to protect, so the caller can skip the
        /// whole transaction.
        /// </summary>
        public static bool ShouldStage(string liveDirectory) => Directory.Exists(liveDirectory);
    }
}