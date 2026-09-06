using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    public class PluginInstallerVerifyTests : IDisposable
    {
        private readonly string _tempDir;

        public PluginInstallerVerifyTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Fact]
        public void VerifyPackageHash_MatchingHash_ReturnsTrue()
        {
            string packagePath = Path.Combine(_tempDir, "test.rspkg");
            File.WriteAllBytes(packagePath, new byte[] { 0x01, 0x02, 0x03 });

            string expectedHash = Bloxstrap.Utility.SHA256Hash.FromFile(packagePath);

            bool result = PluginInstaller.VerifyPackageHash(packagePath, expectedHash);

            Assert.True(result);
        }

        [Fact]
        public void VerifyPackageHash_MismatchingHash_ReturnsFalse()
        {
            string packagePath = Path.Combine(_tempDir, "test.rspkg");
            File.WriteAllBytes(packagePath, new byte[] { 0x01, 0x02, 0x03 });

            bool result = PluginInstaller.VerifyPackageHash(packagePath, "0000000000000000000000000000000000000000000000000000000000000000");

            Assert.False(result);
        }

        [Fact]
        public void VerifyPackageHash_NonexistentFile_ReturnsFalse()
        {
            bool result = PluginInstaller.VerifyPackageHash(
                Path.Combine(_tempDir, "nonexistent.rspkg"),
                "0000000000000000000000000000000000000000000000000000000000000000");

            Assert.False(result);
        }

        [Fact]
        public void VerifyPackageHash_CaseInsensitiveHash_ReturnsTrue()
        {
            string packagePath = Path.Combine(_tempDir, "test.rspkg");
            File.WriteAllBytes(packagePath, new byte[] { 0x01, 0x02, 0x03 });

            string hash = Bloxstrap.Utility.SHA256Hash.FromFile(packagePath);
            string upperHash = hash.ToUpperInvariant();

            bool result = PluginInstaller.VerifyPackageHash(packagePath, upperHash);

            Assert.True(result);
        }
    }
}
