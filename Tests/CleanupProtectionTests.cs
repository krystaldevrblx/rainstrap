
namespace Rainstrap.Tests
{
    /// <summary>
    /// Cleanup policy: a version the user chose or kept must never be deleted by
    /// automatic maintenance.
    ///
    /// The old behaviour deleted every folder except the one recorded as
    /// installed, which made Version Control impossible - a downloaded but not
    /// yet selected version was destroyed on the very next launch.
    /// </summary>
    public class CleanupProtectionTests : IDisposable
    {
        private readonly TempInstall _temp = new();

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void Protection_IncludesTheSelectedVersion()
        {
            VersionControl.SelectVersion("version-selected");

            Assert.Contains("version-selected", VersionControl.GetProtectedVersionGuids());
        }

        [Fact]
        public void Protection_IncludesTheInstalledVersion()
        {
            _temp.SetInstalled("version-installed");

            Assert.Contains("version-installed", VersionControl.GetProtectedVersionGuids());
        }

        [Fact]
        public void Protection_IncludesRetainedVersions()
        {
            VersionControl.RetainVersion("version-kept");

            Assert.Contains("version-kept", VersionControl.GetProtectedVersionGuids());
        }

        [Fact]
        public void Protection_IncludesStudio()
        {
            App.RobloxState.Prop.Studio.VersionGuid = "version-studio";

            Assert.Contains("version-studio", VersionControl.GetProtectedVersionGuids());
        }

        [Fact]
        public void Protection_NormalizesGuids()
        {
            VersionControl.SelectVersion("selectedraw");

            Assert.Contains("version-selectedraw", VersionControl.GetProtectedVersionGuids());
        }

        [Fact]
        public void SelectingAVersionDropsItFromTheRetainedList()
        {
            VersionControl.RetainVersion("version-x");
            VersionControl.SelectVersion("version-x");

            // It is protected by being selected now; keeping a second record of it
            // would be redundant state that can drift.
            Assert.DoesNotContain("version-x", App.RobloxState.Prop.RetainedPlayerVersionGuids);
        }

        // ---------- deletion decisions ----------

        [Fact]
        public void Deletion_IsRefusedForTheSelectedVersion()
        {
            VersionControl.SelectVersion("version-selected");

            var protectedGuids = VersionControl.GetProtectedVersionGuids();

            Assert.False(VersionControl.CanDeleteVersion("version-selected", protectedGuids, isRunning: false));
        }

        [Fact]
        public void Deletion_IsRefusedForARetainedVersion()
        {
            VersionControl.RetainVersion("version-kept");

            var protectedGuids = VersionControl.GetProtectedVersionGuids();

            Assert.False(VersionControl.CanDeleteVersion("version-kept", protectedGuids, isRunning: false));
        }

        [Fact]
        public void Deletion_IsRefusedWhileTheVersionIsRunning()
        {
            // Even an unprotected folder is off limits while a live client is
            // executing out of it.
            Assert.False(VersionControl.CanDeleteVersion("version-anything", new HashSet<string>(), isRunning: true));
        }

        [Fact]
        public void Deletion_IsAllowedForAnUnprotectedFolder()
        {
            Assert.True(VersionControl.CanDeleteVersion("version-orphan", new HashSet<string>(), isRunning: false));
        }

        [Fact]
        public void Deletion_MatchesGuidsRegardlessOfPrefixSpelling()
        {
            VersionControl.SelectVersion("version-selected");

            var protectedGuids = VersionControl.GetProtectedVersionGuids();

            // The folder on disk might be named without the prefix.
            Assert.False(VersionControl.CanDeleteVersion("selected", protectedGuids, isRunning: false));
        }

        [Fact]
        public void Deletion_RefusesABlankFolderName()
        {
            Assert.False(VersionControl.CanDeleteVersion(string.Empty, new HashSet<string>(), isRunning: false));
        }

        // ---------- the real sweep ----------

        [Fact]
        public void Cleanup_DoesNotDeleteTheSelectedVersion()
        {
            _temp.CreateInstall("version-selected");
            _temp.CreateInstall("version-installed");
            _temp.SetInstalled("version-installed");
            VersionControl.SelectVersion("version-selected");

            Bootstrapper.CleanupVersionsFolder();

            // Both survive: one is selected, the other is installed. This is the
            // case the old policy got wrong.
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-selected")));
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-installed")));
        }

        [Fact]
        public void Cleanup_KeepsAnInstalledButUnselectedVersion()
        {
            // Removing a real version is a deliberate user action on the Version
            // Control page, never a side effect of installing another one.
            _temp.CreateInstall("version-switched-away-from");
            _temp.SetInstalled("version-current");
            VersionControl.SelectVersion("version-current");

            Bootstrapper.CleanupVersionsFolder();

            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-switched-away-from")));
        }

        [Fact]
        public void Cleanup_KeepsRetainedVersions()
        {
            _temp.CreateInstall("version-retained");
            VersionControl.RetainVersion("version-retained");

            Bootstrapper.CleanupVersionsFolder();

            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-retained")));
        }

        [Fact]
        public void Cleanup_RemovesIncompleteDownloads()
        {
            _temp.CreateIncompleteInstall("version-interrupted");

            Bootstrapper.CleanupVersionsFolder();

            // A folder with no client in it can never launch, so reclaiming it
            // automatically is the one safe automatic deletion.
            Assert.False(Directory.Exists(Path.Combine(_temp.Versions, "version-interrupted")));
        }

        [Fact]
        public void Cleanup_NeverRemovesTheOnlyWorkingVersion()
        {
            _temp.CreateInstall("version-only");
            _temp.SetInstalled("version-only");

            Bootstrapper.CleanupVersionsFolder();

            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-only")));
        }

        [Fact]
        public void Cleanup_KeepsStudioInstallations()
        {
            _temp.CreateInstall("version-studio", "RobloxStudioBeta.exe");
            App.RobloxState.Prop.Studio.VersionGuid = "version-studio";

            Bootstrapper.CleanupVersionsFolder();

            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-studio")));
        }

        // ---------- explicit removal ----------

        [Fact]
        public void ExplicitRemoval_RefusesTheSelectedVersion()
        {
            _temp.CreateInstall("version-selected");
            VersionControl.SelectVersion("version-selected");

            string? failure = Bootstrapper.RemoveInstalledVersion("version-selected", out _);

            Assert.NotNull(failure);
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-selected")));
        }

        [Fact]
        public void ExplicitRemoval_RefusesTheInstalledVersion()
        {
            _temp.CreateInstall("version-installed");
            _temp.SetInstalled("version-installed");

            string? failure = Bootstrapper.RemoveInstalledVersion("version-installed", out _);

            Assert.NotNull(failure);
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-installed")));
        }

        [Fact]
        public void ExplicitRemoval_DeletesAnUnprotectedVersion()
        {
            _temp.CreateInstall("version-unused");

            string? failure = Bootstrapper.RemoveInstalledVersion("version-unused", out string removed);

            Assert.Null(failure);
            Assert.Equal("version-unused", removed);
            Assert.False(Directory.Exists(Path.Combine(_temp.Versions, "version-unused")));
        }

        [Fact]
        public void ExplicitRemoval_ReportsAVersionThatIsNotInstalled()
        {
            string? failure = Bootstrapper.RemoveInstalledVersion("version-nope", out _);

            Assert.NotNull(failure);
        }
    }
}