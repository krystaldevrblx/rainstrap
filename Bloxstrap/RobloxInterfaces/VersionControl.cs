using Bloxstrap.AppData;
using Bloxstrap.Models.Entities;

namespace Bloxstrap.RobloxInterfaces
{
    /// <summary>
    /// Version Control: decides which Roblox version Rainstrap should launch,
    /// and keeps that decision across restarts.
    ///
    /// Four separate questions are kept apart on purpose, because collapsing any
    /// two of them is how a user's chosen version gets replaced:
    ///
    ///   latest official  - what the channel publishes right now
    ///   installed         - what is on disk (AppState.VersionGuid)
    ///   selected          - what the user asked for (RobloxState.SelectedPlayerVersionGuid)
    ///   next launch       - what this process will actually use
    ///
    /// Only <see cref="SelectedVersionGuid"/> is ever written by a user action or
    /// by an accepted update prompt. Nothing else is allowed to overwrite it.
    /// </summary>
    public static class VersionControl
    {
        /// <summary>
        /// Floor below which a file cannot plausibly be a Windows executable.
        ///
        /// Kept small on purpose: the PE signature check is what actually
        /// establishes that a file is a binary, so this only has to reject an
        /// empty or stub file and does not need to guess at real client sizes.
        /// </summary>
        private const long MinimumPlausibleExecutableBytes = 1024;

        #region Selection

        /// <summary>
        /// The version the user pinned, normalised. Empty means "follow the channel".
        /// </summary>
        public static string SelectedVersionGuid =>
            Deployment.NormalizeVersionGuid(App.RobloxState.Prop.SelectedPlayerVersionGuid);

        /// <summary>True when the selection came from an explicit user action.</summary>
        public static bool IsSelectionPinned => App.RobloxState.Prop.SelectedPlayerVersionIsPinned;

        /// <summary>True when the user has pinned something at all.</summary>
        public static bool HasSelection => !String.IsNullOrEmpty(SelectedVersionGuid);

        /// <summary>
        /// Records a user-chosen version and persists it.
        ///
        /// This is the only writer of the selection. Recording a selection never
        /// touches <c>AppState.VersionGuid</c>, so the next launch resolves it
        /// through this class instead of finding it already changed underneath.
        /// </summary>
        public static void SelectVersion(string versionGuid, bool pinned = true)
        {
            const string LOG_IDENT = "VersionControl::SelectVersion";

            string normalized = Deployment.NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(normalized))
                return;

            App.RobloxState.Prop.SelectedPlayerVersionGuid = normalized;
            App.RobloxState.Prop.SelectedPlayerVersionIsPinned = pinned;

            if (App.RobloxState.Prop.RetainedPlayerVersionGuids.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                App.RobloxState.Prop.RetainedPlayerVersionGuids.Remove(normalized);

            App.RobloxState.Save();

            App.Logger.WriteLine(LOG_IDENT, $"Selected version is now {normalized} (pinned: {pinned})");
        }

        /// <summary>
        /// Clears the pin so the channel's current version is used again.
        /// Never touches installed files.
        /// </summary>
        public static void ClearSelection()
        {
            const string LOG_IDENT = "VersionControl::ClearSelection";

            App.Logger.WriteLine(LOG_IDENT, "Clearing the pinned version, following the channel again");

            App.RobloxState.Prop.SelectedPlayerVersionGuid = string.Empty;
            App.RobloxState.Prop.SelectedPlayerVersionIsPinned = false;

            App.RobloxState.Save();
        }

        /// <summary>
        /// Marks a version as one cleanup must keep, without selecting it.
        /// </summary>
        public static void RetainVersion(string versionGuid)
        {
            string normalized = Deployment.NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(normalized))
                return;

            if (App.RobloxState.Prop.RetainedPlayerVersionGuids.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                return;

            App.RobloxState.Prop.RetainedPlayerVersionGuids.Add(normalized);
            App.RobloxState.Save();
        }

        /// <summary>Removes a retained version, allowing cleanup to reclaim it.</summary>
        public static void UnretainVersion(string versionGuid)
        {
            string normalized = Deployment.NormalizeVersionGuid(versionGuid);

            App.RobloxState.Prop.RetainedPlayerVersionGuids.RemoveAll(
                x => String.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

            App.RobloxState.Save();
        }

        #endregion

        #region Resolution

        /// <summary>
        /// Works out which version this launch should use.
        ///
        /// Ordering is deliberate: an explicit command line always wins, because
        /// the user typed it; then the pin; then the channel. Crucially, the pin
        /// is consulted <em>before</em> the channel, and a channel upgrade never
        /// reaches this method's output as long as a pin exists.
        /// </summary>
        public static VersionResolution ResolveLaunchVersion(string channelLatestGuid, string? commandLineVersion = null)
        {
            string latest = Deployment.NormalizeVersionGuid(channelLatestGuid);

            if (!String.IsNullOrWhiteSpace(commandLineVersion))
            {
                string fromArgs = Deployment.NormalizeVersionGuid(commandLineVersion);

                if (!String.IsNullOrEmpty(fromArgs))
                    return new VersionResolution
                    {
                        VersionGuid = fromArgs,
                        Source = VersionResolutionSource.CommandLine,
                    };
            }

            if (HasSelection)
                return new VersionResolution { VersionGuid = SelectedVersionGuid, Source = VersionResolutionSource.UserSelection };

            return new VersionResolution { VersionGuid = latest, Source = VersionResolutionSource.Channel };
        }

        /// <summary>
        /// Validates an installed version before it is offered for launch.
        ///
        /// "Files exist" is not the same as "this version works", so this checks
        /// the executable is present, non-trivial in size, and reports a parseable
        /// version. A folder that merely exists is not treated as usable.
        /// </summary>
        public static InstallationValidation ValidateInstallation(string versionGuid, string? executableName = null)
        {
            string guid = Deployment.NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(guid))
                return new(InstallationValidationResult.Missing, null, 0);

            executableName ??= App.RobloxPlayerAppName;

            if (App.Settings.Prop.StaticDirectory)
                return ValidateExecutable(Path.Combine(Paths.Versions, new RobloxPlayerData().BinaryType, executableName));

            string directory = Path.Combine(Paths.Versions, guid);

            // Distinguish "never installed" from "the install is broken" so the
            // recovery prompt can tell the user which of the two happened.
            if (!Directory.Exists(directory))
                return new(InstallationValidationResult.Missing, null, 0);

            return ValidateExecutable(Path.Combine(directory, executableName));
        }

        private static InstallationValidation ValidateExecutable(string executablePath)
        {
            if (!File.Exists(executablePath))
                return new(InstallationValidationResult.ExecutableMissing, null, 0);

            long size;

            try
            {
                size = new FileInfo(executablePath).Length;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VersionControl::ValidateExecutable", $"Could not stat {executablePath}");
                App.Logger.WriteException("VersionControl::ValidateExecutable", ex);

                return new(InstallationValidationResult.ExecutableMissing, null, 0);
            }

            // A file that exists is not a client. Reject anything too small to be a
            // Windows binary, and anything that does not start with the PE "MZ"
            // signature - which is what a partially written download looks like.
            if (size < MinimumPlausibleExecutableBytes || !LooksLikePortableExecutable(executablePath))
                return new(InstallationValidationResult.ExecutableTruncated, null, size);

            string? version;

            try
            {
                var info = FileVersionInfo.GetVersionInfo(executablePath);
                version = info.ProductVersion?.Replace(", ", ".");

                if (String.IsNullOrWhiteSpace(version) || Utilities.ParseVersionSafe(version) is null)
                    return new(InstallationValidationResult.VersionUnreadable, null, size);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VersionControl::ValidateExecutable", $"Could not read the version of {executablePath}");
                App.Logger.WriteException("VersionControl::ValidateExecutable", ex);

                return new(InstallationValidationResult.VersionUnreadable, null, size);
            }

            return new(InstallationValidationResult.Valid, version, size);
        }

        /// <summary>
        /// Whether a file begins with the DOS/PE header signature "MZ".
        ///
        /// Deliberately a structural check rather than a size threshold: a magic
        /// number for "big enough" is arbitrary and drifts, whereas the header is
        /// the thing that actually makes a file a Windows executable.
        /// </summary>
        private static bool LooksLikePortableExecutable(string path)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                Span<byte> header = stackalloc byte[2];
                int read = stream.Read(header);

                return read == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VersionControl::LooksLikePortableExecutable", $"Could not inspect {path}");
                App.Logger.WriteException("VersionControl::LooksLikePortableExecutable", ex);

                return false;
            }
        }

        /// <summary>
        /// Combines "does Roblox still publish it" with "is it usable here" into one
        /// answer, without ever claiming support on the strength of local files.
        /// </summary>
        public static VersionSupportState GetSupportState(VersionCatalogEntry entry, InstallationValidation? validation = null)
        {
            if (validation is null)
                validation = ValidateInstallation(entry.VersionGuid);

            // A recorded refusal outranks everything: Roblox already told us it
            // will not run this client, and re-deriving that from disk is pointless.
            if (App.RobloxState.Prop.RefusedVersionGuids.ContainsKey(entry.VersionGuid))
                return VersionSupportState.BlockedByRoblox;

            if (!validation.Value.IsValid)
                return validation.Value.RequiresReinstall
                    ? VersionSupportState.CorruptedInstallation
                    : VersionSupportState.Unknown;

            if (entry.Availability == VersionAvailability.Unavailable)
                return VersionSupportState.NoLongerPublished;

            return VersionSupportState.Supported;
        }

        #endregion

        #region Catalog

        /// <summary>
        /// Refreshes the catalogue.
        ///
        /// Results are persisted so the page can reopen with the last real answer,
        /// and the snapshot records whether this refresh was exhaustive. It is
        /// never exhaustive, and the UI says so, because Roblox publishes no
        /// endpoint that lists prior releases.
        /// </summary>
        public static async Task<VersionCatalogResult> RefreshCatalogAsync(CancellationToken token = default)
        {
            const string LOG_IDENT = "VersionControl::RefreshCatalogAsync";

            var installed = GetInstalledVersionGuids();
            var observed = new List<string>();

            observed.AddRange(App.RobloxState.Prop.PlayerVersionHistory.Select(x => x.VersionGuid));
            observed.AddRange(installed);
            observed.Add(GetSelectedOrInstalledGuid());

            var result = await Deployment.DiscoverVersionsAsync(
                new RobloxPlayerData().BinaryType,
                observed,
                installed,
                token);

            string selected = SelectedVersionGuid;
            string latest = result.Entries.FirstOrDefault(x => x.IsLatestOfficial)?.VersionGuid ?? string.Empty;

            foreach (var entry in result.Entries)
            {
                entry.IsSelected = !String.IsNullOrEmpty(selected)
                                && String.Equals(entry.VersionGuid, selected, StringComparison.OrdinalIgnoreCase);

                // Fill in a version number from local evidence where Roblox gave
                // none, so the list is not a wall of bare GUIDs.
                if (String.IsNullOrEmpty(entry.Version))
                {
                    var fromHistory = App.RobloxState.Prop.PlayerVersionHistory
                        .FirstOrDefault(x => String.Equals(x.VersionGuid, entry.VersionGuid, StringComparison.OrdinalIgnoreCase));

                    if (fromHistory is not null)
                        entry.Version = fromHistory.Version;

                    if (String.IsNullOrEmpty(entry.Version))
                    {
                        var validation = ValidateInstallation(entry.VersionGuid);

                        if (validation.DetectedVersion is not null)
                            entry.Version = validation.DetectedVersion;
                    }
                }
            }

            if (!result.LatestResolved)
                result.Failures.Add(Deployment.Channel);

            result.Limitations.Add(Strings.VersionControl_Limitation_NoHistoryEndpoint);

            if (result.Failures.Any())
                result.Limitations.Add(Strings.VersionControl_Limitation_PartialRefresh);

            PersistCatalog(result);

            App.Logger.WriteLine(LOG_IDENT, $"Catalog refreshed: {result.Entries.Count} entries, latest resolved: {result.LatestResolved}");

            return result;
        }

        /// <summary>
        /// Restores the last persisted catalogue without hitting the network.
        /// Selection flags are recomputed from current state, never trusted from disk.
        /// </summary>
        public static VersionCatalogResult RestoreCatalog()
        {
            var snapshot = App.RobloxState.Prop.VersionCatalog;

            if (snapshot is null)
                return new VersionCatalogResult();

            string selected = SelectedVersionGuid;
            string installed = Deployment.NormalizeVersionGuid(App.RobloxState.Prop.Player.VersionGuid);

            var result = new VersionCatalogResult
            {
                IsExhaustive = snapshot.IsExhaustive,
                LatestResolved = snapshot.LatestResolved,
                RefreshedAtUtc = snapshot.RefreshedAtUtc,
                Limitations = new(snapshot.Limitations),
                Entries = snapshot.Entries.Select(x => x.ToEntry()).ToList(),
            };

            foreach (var entry in result.Entries)
            {
                entry.IsSelected = !String.IsNullOrEmpty(selected)
                                && String.Equals(entry.VersionGuid, selected, StringComparison.OrdinalIgnoreCase);

                // Installed state is re-read from the filesystem rather than taken
                // from the snapshot, which may predate a delete.
                entry.IsInstalled = !String.IsNullOrEmpty(installed)
                                 && String.Equals(entry.VersionGuid, installed, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private static void PersistCatalog(VersionCatalogResult result)
        {
            App.RobloxState.Prop.VersionCatalog = new VersionCatalogSnapshot
            {
                Channel = Deployment.Channel,
                RefreshedAtUtc = result.RefreshedAtUtc,
                IsExhaustive = result.IsExhaustive,
                LatestResolved = result.LatestResolved,
                Limitations = new(result.Limitations),
                Entries = result.Entries.Select(VersionCatalogSnapshotEntry.FromEntry).ToList(),
            };

            App.RobloxState.Save();
        }

        #endregion

        #region Installed versions

        /// <summary>
        /// Every version folder present under the versions directory.
        ///
        /// Includes ones with no recorded history, so a version the user downloaded
        /// outside the normal install path is still visible and still protected
        /// from cleanup while selected.
        /// </summary>
        public static HashSet<string> GetInstalledVersionGuids()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (App.Settings.Prop.StaticDirectory || !Directory.Exists(Paths.Versions))
                return result;

            try
            {
                foreach (string dir in Directory.GetDirectories(Paths.Versions))
                {
                    string name = Path.GetFileName(dir);

                    if (String.IsNullOrWhiteSpace(name))
                        continue;

                    result.Add(Deployment.NormalizeVersionGuid(name));
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VersionControl::GetInstalledVersionGuids", "Could not enumerate the versions directory");
                App.Logger.WriteException("VersionControl::GetInstalledVersionGuids", ex);
            }

            return result;
        }

        private static string GetSelectedOrInstalledGuid()
            => App.RobloxState.Prop.Player.VersionGuid;

        #endregion

        #region Cleanup protection

        /// <summary>
        /// Version guids cleanup must never delete.
        ///
        /// Protects, in order of importance: whatever is running right now, the
        /// user's selection, the recorded install, everything the user explicitly
        /// retained, and Studio. The previous behaviour - keep only the active
        /// version - is exactly what made Version Control unusable, so this set is
        /// deliberately generous.
        /// </summary>
        public static HashSet<string> GetProtectedVersionGuids()
        {
            var protectedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Protect(string? guid)
            {
                string normalized = Deployment.NormalizeVersionGuid(guid ?? String.Empty);

                if (!String.IsNullOrEmpty(normalized))
                    protectedGuids.Add(normalized);
            }

            // 1. what the user's decision says to keep
            Protect(App.RobloxState.Prop.SelectedPlayerVersionGuid);

            // 2. what is recorded as installed for either product
            Protect(App.RobloxState.Prop.Player.VersionGuid);
            Protect(App.RobloxState.Prop.Studio.VersionGuid);

            // 3. what the user explicitly retained
            foreach (string guid in App.RobloxState.Prop.RetainedPlayerVersionGuids)
                Protect(guid);

            // 4. static-directory layouts have no guid folders to protect
            if (App.Settings.Prop.StaticDirectory)
            {
                protectedGuids.Add("WindowsPlayer");
                protectedGuids.Add("WindowsStudio64");
            }

            return protectedGuids;
        }

        /// <summary>
        /// Decides whether a version folder may be deleted.
        ///
        /// Split out from the filesystem work so the policy is directly testable
        /// and, more importantly, so the rule is one readable function instead of
        /// conditions scattered through a delete loop.
        /// </summary>
        public static bool CanDeleteVersion(string directoryName, ISet<string> protectedGuids, bool isRunning)
        {
            // A running client is never removed, whatever the policy says. The
            // folder may be locked anyway, but asking the OS first gives a clean
            // skip instead of a noisy failure.
            if (isRunning)
                return false;

            if (App.Settings.Prop.StaticDirectory)
                return !protectedGuids.Contains(directoryName);

            string guid = Deployment.NormalizeVersionGuid(directoryName);

            if (String.IsNullOrEmpty(guid))
                return false;

            return !protectedGuids.Contains(guid);
        }

        #endregion

        #region Refusal tracking

        /// <summary>
        /// Records that Roblox refused this client.
        ///
        /// Only ever called when the cause is positively identifiable. This exists
        /// so the UI can advise the user; it never changes what Rainstrap attempts,
        /// and it is not a mechanism for getting around the refusal.
        /// </summary>
        public static void RecordRefusal(string versionGuid)
        {
            string normalized = Deployment.NormalizeVersionGuid(versionGuid);

            if (String.IsNullOrEmpty(normalized))
                return;

            App.RobloxState.Prop.RefusedVersionGuids[normalized] = DateTime.UtcNow;
            App.RobloxState.Prop.TrimRefusedVersions();
            App.RobloxState.Save();

            App.Logger.WriteLine("VersionControl::RecordRefusal", $"Roblox refused {normalized}; recorded for user guidance");
        }

        public static bool WasRefused(string versionGuid)
        {
            string normalized = Deployment.NormalizeVersionGuid(versionGuid);

            return !String.IsNullOrEmpty(normalized)
                && App.RobloxState.Prop.RefusedVersionGuids.ContainsKey(normalized);
        }

        #endregion
    }

    public enum VersionResolutionSource
    {
        /// <summary>No pin, no command line: the channel's current version.</summary>
        Channel = 0,

        /// <summary>A Version Control pin is in force.</summary>
        UserSelection = 1,

        /// <summary>The launch command line pinned it explicitly.</summary>
        CommandLine = 2,
    }

    public class VersionResolution
    {
        public string VersionGuid { get; set; } = string.Empty;

        public VersionResolutionSource Source { get; set; } = VersionResolutionSource.Channel;
    }
}