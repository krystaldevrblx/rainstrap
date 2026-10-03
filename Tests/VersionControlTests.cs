
namespace Rainstrap.Tests
{
    /// <summary>
    /// The core promise: a chosen version survives restarts, is never silently
    /// replaced, and is never deleted by automatic cleanup.
    /// </summary>
    public class VersionControlTests : IDisposable
    {
        private readonly TempInstall _temp = new();

        public void Dispose() => _temp.Dispose();

        // ---------- selection persistence ----------

        [Fact]
        public void Selection_IsPersistedAndSurvivesAReload()
        {
            _temp.Select("version-02c37bc51a384b8f");

            // Simulate a restart: drop every in-memory object and read the
            // persisted state back from disk, exactly as App.OnStartup does.
            _temp.SetInstalled(string.Empty);
            VersionControl.SelectVersion("version-02c37bc51a384b8f");

            App.RobloxState.Load();

            Assert.Equal("version-02c37bc51a384b8f", VersionControl.SelectedVersionGuid);
            Assert.True(VersionControl.HasSelection);
        }

        [Fact]
        public void Selection_IsMarkedPinnedOnlyForExplicitUserChoices()
        {
            VersionControl.SelectVersion("version-aaa", pinned: true);
            Assert.True(VersionControl.IsSelectionPinned);

            VersionControl.SelectVersion("version-bbb", pinned: false);
            Assert.False(VersionControl.IsSelectionPinned);
        }

        [Fact]
        public void Selection_SurvivesAnOrdinaryUpdateOfTheInstalledVersion()
        {
            // This is the regression that motivated the feature: installing a new
            // version updated AppState.VersionGuid, and previously that was the
            // same field the choice lived in, so the choice was overwritten.
            VersionControl.SelectVersion("version-olduserchoice");

            _temp.SetInstalled("version-newlyinstalled");

            Assert.Equal("version-olduserchoice", VersionControl.SelectedVersionGuid);
            Assert.Equal("version-newlyinstalled", App.RobloxState.Prop.Player.VersionGuid);
        }

        [Fact]
        public void Selection_IsNormalizedOnTheWayIn()
        {
            VersionControl.SelectVersion("02c37bc51a384b8f");

            Assert.Equal("version-02c37bc51a384b8f", VersionControl.SelectedVersionGuid);
        }

        [Fact]
        public void ClearSelection_LeavesInstalledFilesAlone()
        {
            _temp.CreateInstall("version-abc");
            _temp.SetInstalled("version-abc");
            VersionControl.SelectVersion("version-abc");

            VersionControl.ClearSelection();

            Assert.False(VersionControl.HasSelection);

            // Following the channel again must never be a way to delete files.
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-abc")));
        }

        // ---------- launch resolution ----------

        [Fact]
        public void Resolution_FollowsTheChannelWhenNothingIsPinned()
        {
            var resolution = VersionControl.ResolveLaunchVersion("version-latest");

            Assert.Equal("version-latest", resolution.VersionGuid);
            Assert.Equal(VersionResolutionSource.Channel, resolution.Source);
        }

        [Fact]
        public void Resolution_PrefersTheUserSelectionOverANewerChannelRelease()
        {
            VersionControl.SelectVersion("version-older");

            var resolution = VersionControl.ResolveLaunchVersion("version-newer");

            Assert.Equal("version-older", resolution.VersionGuid);
            Assert.Equal(VersionResolutionSource.UserSelection, resolution.Source);
        }

        [Fact]
        public void Resolution_CommandLineBeatsEverything()
        {
            VersionControl.SelectVersion("version-pinned");

            var resolution = VersionControl.ResolveLaunchVersion("version-latest", "version-explicit");

            // Someone who typed a version did not ask to be offered another one.
            Assert.Equal("version-explicit", resolution.VersionGuid);
            Assert.Equal(VersionResolutionSource.CommandLine, resolution.Source);
        }

        [Fact]
        public void Resolution_BlankCommandLineIsIgnored()
        {
            VersionControl.SelectVersion("version-pinned");

            var resolution = VersionControl.ResolveLaunchVersion("version-latest", "   ");

            Assert.Equal("version-pinned", resolution.VersionGuid);
        }

        // ---------- validation ----------

        [Fact]
        public void Validation_MissingVersionIsReportedNotGuessed()
        {
            var validation = VersionControl.ValidateInstallation("version-notinstalled");

            Assert.Equal(InstallationValidationResult.Missing, validation.Result);
            Assert.False(validation.IsValid);
        }

        [Fact]
        public void Validation_ACompleteInstallIsReportedValid()
        {
            _temp.CreateInstall("version-good");

            var validation = VersionControl.ValidateInstallation("version-good");

            // The baseline the other validation tests depend on: if this fails,
            // every "installed version survives cleanup" assertion would be
            // passing only because the install looked broken.
            Assert.Equal(InstallationValidationResult.Valid, validation.Result);
            Assert.True(validation.IsValid);
            Assert.False(validation.RequiresReinstall);
            Assert.NotNull(validation.DetectedVersion);
            Assert.True(validation.SizeBytes > 0);
        }

        [Fact]
        public void Validation_MissingVersionFolderIsDistinctFromABrokenInstall()
        {
            var missing = VersionControl.ValidateInstallation("version-neverinstalled");

            // No folder at all.
            Assert.Equal(InstallationValidationResult.Missing, missing.Result);

            _temp.CreateIncompleteInstall("version-partial");

            var broken = VersionControl.ValidateInstallation("version-partial");

            // Folder present, no client in it. This is the shape an interrupted
            // download leaves behind, and it must never be presented as usable.
            Assert.Equal(InstallationValidationResult.ExecutableMissing, broken.Result);
            Assert.False(broken.IsValid);
            Assert.True(broken.RequiresReinstall);
        }

        [Fact]
        public void Validation_TruncatedExecutableIsNotTreatedAsInstalled()
        {
            // A file that exists is not the same as a usable client.
            _temp.CreateInstall("version-truncated", sizeBytes: 4096);

            var validation = VersionControl.ValidateInstallation("version-truncated");

            Assert.Equal(InstallationValidationResult.ExecutableTruncated, validation.Result);
            Assert.True(validation.RequiresReinstall);
        }

        [Fact]
        public void Validation_ExecutableWithoutAVersionIsRejected()
        {
            // Structurally a PE, large enough, but carrying no version resource:
            // still not a client Rainstrap can identify, and not something a
            // reinstall is guaranteed to fix.
            string directory = Path.Combine(_temp.Versions, "version-nometa");

            Directory.CreateDirectory(directory);

            using (var stream = new FileStream(
                       Path.Combine(directory, "RobloxPlayerBeta.exe"), FileMode.Create, FileAccess.Write))
            {
                stream.Write(new byte[] { (byte)'M', (byte)'Z' }, 0, 2);
                stream.SetLength(64 * 1024);
            }

            var validation = VersionControl.ValidateInstallation("version-nometa");

            Assert.Equal(InstallationValidationResult.VersionUnreadable, validation.Result);
            Assert.False(validation.IsValid);
            Assert.False(validation.RequiresReinstall);
        }

        [Fact]
        public void Validation_EmptyGuidIsMissing()
        {
            var validation = VersionControl.ValidateInstallation(string.Empty);

            Assert.Equal(InstallationValidationResult.Missing, validation.Result);
        }

        // ---------- support state ----------

        [Fact]
        public void SupportState_PublishedAndInstalledIsSupported()
        {
            _temp.CreateInstall("version-good");

            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-good",
                Availability = VersionAvailability.Available,
                IsInstalled = true,
            };

            var state = VersionControl.GetSupportState(
                entry,
                new InstallationValidation(InstallationValidationResult.Valid, "0.700.0.7001", 1_000_000));

            Assert.Equal(VersionSupportState.Supported, state);
        }

        [Fact]
        public void SupportState_ValidFilesButWithdrawnFromTheCdnIsNotSupported()
        {
            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-withdrawn",
                Availability = VersionAvailability.Unavailable,
                IsInstalled = true,
            };

            var state = VersionControl.GetSupportState(
                entry,
                new InstallationValidation(InstallationValidationResult.Valid, "0.700.0.7001", 1_000_000));

            // Files on disk are not proof Roblox still accepts the client.
            Assert.Equal(VersionSupportState.NoLongerPublished, state);
        }

        [Fact]
        public void SupportState_BrokenInstallIsReportedAsCorrupted()
        {
            _temp.CreateIncompleteInstall("version-broken");

            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-broken",
                Availability = VersionAvailability.Available,
            };

            Assert.Equal(VersionSupportState.CorruptedInstallation, VersionControl.GetSupportState(entry));
        }

        [Fact]
        public void SupportState_ARecordedRefusalOutranksEverything()
        {
            _temp.CreateInstall("version-refused");
            VersionControl.RecordRefusal("version-refused");

            var entry = new VersionCatalogEntry
            {
                VersionGuid = "version-refused",
                Availability = VersionAvailability.Available,
                IsInstalled = true,
            };

            var state = VersionControl.GetSupportState(
                entry,
                new InstallationValidation(InstallationValidationResult.Valid, "0.700.0.7001", 1_000_000));

            Assert.Equal(VersionSupportState.BlockedByRoblox, state);
            Assert.True(VersionControl.WasRefused("version-refused"));
        }

        [Fact]
        public void Refusals_AreBounded()
        {
            for (int i = 0; i < 60; i++)
                VersionControl.RecordRefusal($"version-{i:D3}");

            Assert.True(App.RobloxState.Prop.RefusedVersionGuids.Count <= 20);
        }

        // ---------- installed version enumeration ----------

        [Fact]
        public void InstalledVersions_AreDiscoveredFromDiskNotJustFromHistory()
        {
            // A version downloaded outside the normal install path has no history
            // entry. It must still be visible to Version Control.
            _temp.CreateInstall("version-orphan");

            var installed = VersionControl.GetInstalledVersionGuids();

            Assert.Contains("version-orphan", installed);
        }

        [Fact]
        public void InstalledVersions_AreNormalized()
        {
            Directory.CreateDirectory(Path.Combine(_temp.Versions, "rawguid"));

            Assert.Contains("version-rawguid", VersionControl.GetInstalledVersionGuids());
        }
    }
}