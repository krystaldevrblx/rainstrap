
namespace Rainstrap.Tests
{
    /// <summary>
    /// The guarantee that a failed install never costs the user their working
    /// client. Exercised against real temporary directories.
    /// </summary>
    public class InstallTransactionTests : IDisposable
    {
        private readonly TempInstall _temp = new();
        private readonly string _root;

        public InstallTransactionTests()
        {
            _root = Path.Combine(_temp.Versions, "version-target");
        }

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void Staging_MovesTheExistingCopyAsideRatherThanDeletingIt()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "working client");

            string? staged = VersionInstallTransaction.Stage(_root);

            Assert.NotNull(staged);
            Assert.True(Directory.Exists(staged!));
            Assert.False(Directory.Exists(_root));
            Assert.True(File.Exists(Path.Combine(staged!, "RobloxPlayerBeta.exe")));
        }

        [Fact]
        public void Staging_ReturnsNullWhenThereIsNothingToProtect()
        {
            Assert.Null(VersionInstallTransaction.Stage(_root));
            Assert.False(VersionInstallTransaction.ShouldStage(_root));
        }

        [Fact]
        public void AFailedInstallRestoresThePreviousVersion()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "working client");

            string? staged = VersionInstallTransaction.Stage(_root);

            // The replacement download starts and dies partway.
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "truncated");

            bool restored = VersionInstallTransaction.Rollback(_root, staged);

            Assert.True(restored);
            Assert.True(File.Exists(Path.Combine(_root, "RobloxPlayerBeta.exe")));

            // The user's working client is back, and the broken one is gone.
            Assert.Equal("working client", File.ReadAllText(Path.Combine(_root, "RobloxPlayerBeta.exe")));
            Assert.False(Directory.Exists(staged!));
        }

        [Fact]
        public void CommitDiscardsTheStagedCopyAfterASuccessfulInstall()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "old client");

            string? staged = VersionInstallTransaction.Stage(_root);

            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "new client");

            Assert.True(VersionInstallTransaction.Commit(staged));

            // The new install is live and the backup is gone - no leftovers.
            Assert.Equal("new client", File.ReadAllText(Path.Combine(_root, "RobloxPlayerBeta.exe")));
            Assert.False(Directory.Exists(staged!));
        }

        [Fact]
        public void RollbackReportsFailureWhenThereIsNothingToRestore()
        {
            Assert.False(VersionInstallTransaction.Rollback(_root, null));
        }

        [Fact]
        public void CommitReportsNothingToDoWhenNothingWasStaged()
        {
            Assert.False(VersionInstallTransaction.Commit(null));
            Assert.False(VersionInstallTransaction.Commit(Path.Combine(_temp.Versions, "never-existed")));
        }

        [Fact]
        public void ALeftoverStagingFolderIsClearedRatherThanAccumulating()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "RobloxPlayerBeta.exe"), "current client");

            // A previous run died mid-install and left this behind.
            string leftover = _root + VersionInstallTransaction.StagedSuffix;
            Directory.CreateDirectory(leftover);
            File.WriteAllText(Path.Combine(leftover, "stale"), "junk");

            string? staged = VersionInstallTransaction.Stage(_root);

            Assert.NotNull(staged);
            Assert.True(File.Exists(Path.Combine(staged!, "RobloxPlayerBeta.exe")));
            Assert.False(File.Exists(Path.Combine(staged!, "stale")));
        }

        [Fact]
        public void SwitchingBetweenVersionsLeavesBothInstallsIntact()
        {
            // "Switching versions does not corrupt the installation" - a switch is
            // a selection change plus at most an install of the newly selected
            // version; neither may disturb the other folder.
            string a = Path.Combine(_temp.Versions, "version-a");
            string b = Path.Combine(_temp.Versions, "version-b");

            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);
            File.WriteAllText(Path.Combine(a, "RobloxPlayerBeta.exe"), "client a");
            File.WriteAllText(Path.Combine(b, "RobloxPlayerBeta.exe"), "client b");

            VersionControl.SelectVersion("version-a");
            Assert.Equal("version-a", VersionControl.ResolveLaunchVersion("version-b").VersionGuid);

            VersionControl.SelectVersion("version-b");
            Assert.Equal("version-b", VersionControl.ResolveLaunchVersion("version-a").VersionGuid);

            Assert.Equal("client a", File.ReadAllText(Path.Combine(a, "RobloxPlayerBeta.exe")));
            Assert.Equal("client b", File.ReadAllText(Path.Combine(b, "RobloxPlayerBeta.exe")));
        }

        [Fact]
        public void AFailedDownloadPreservesThePreviouslySelectedVersion()
        {
            _temp.CreateInstall("version-working");
            _temp.SetInstalled("version-working");
            VersionControl.SelectVersion("version-working");

            // The replacement never even gets as far as staging.
            Assert.Null(VersionInstallTransaction.Stage(Path.Combine(_temp.Versions, "version-never-created")));

            Assert.Equal("version-working", VersionControl.SelectedVersionGuid);
            Assert.Equal("version-working", App.RobloxState.Prop.Player.VersionGuid);
            Assert.True(Directory.Exists(Path.Combine(_temp.Versions, "version-working")));
        }
    }
}