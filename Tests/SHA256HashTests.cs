using Bloxstrap.Utility;

namespace Rainstrap.Tests
{
    public class SHA256HashTests : IDisposable
    {
        private readonly string _tempDir;

        public SHA256HashTests()
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
        public void FromBytes_ReturnsCorrectHash()
        {
            // SHA-256 of "hello" = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
            byte[] data = System.Text.Encoding.UTF8.GetBytes("hello");
            string hash = SHA256Hash.FromBytes(data);

            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
        }

        [Fact]
        public void FromFile_ReturnsCorrectHash()
        {
            string filePath = Path.Combine(_tempDir, "test.txt");
            File.WriteAllText(filePath, "hello");

            string hash = SHA256Hash.FromFile(filePath);

            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
        }

        [Fact]
        public void FromFile_EmptyFile_ReturnsEmptyHash()
        {
            string filePath = Path.Combine(_tempDir, "empty.txt");
            File.WriteAllBytes(filePath, Array.Empty<byte>());

            string hash = SHA256Hash.FromFile(filePath);

            // SHA-256 of empty = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
        }

        [Fact]
        public void Stringify_ReturnsLowercaseHex()
        {
            byte[] hash = { 0xAB, 0xCD, 0xEF };
            string result = SHA256Hash.Stringify(hash);

            Assert.Equal("abcdef", result);
        }

        [Fact]
        public void ExtractFromReleaseBody_WithValidHash_ReturnsHash()
        {
            string body = @"## What's New
- Feature X added
- Bug fix Y

SHA256: 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

            string? result = SHA256Hash.ExtractFromReleaseBody(body);

            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
        }

        [Fact]
        public void ExtractFromReleaseBody_Uppercase_ReturnsLowercase()
        {
            string body = "SHA256: 2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824";

            string? result = SHA256Hash.ExtractFromReleaseBody(body);

            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
        }

        [Fact]
        public void ExtractFromReleaseBody_NoHash_ReturnsNull()
        {
            string body = "## What's New\n- Feature X added";

            string? result = SHA256Hash.ExtractFromReleaseBody(body);

            Assert.Null(result);
        }

        [Fact]
        public void ExtractFromReleaseBody_InvalidHashLength_ReturnsNull()
        {
            string body = "SHA256: abc123";

            string? result = SHA256Hash.ExtractFromReleaseBody(body);

            Assert.Null(result);
        }

        [Fact]
        public void ExtractFromReleaseBody_NonHexCharacters_ReturnsNull()
        {
            string body = "SHA256: " + new string('g', 64);

            string? result = SHA256Hash.ExtractFromReleaseBody(body);

            Assert.Null(result);
        }

        [Fact]
        public void ExtractFromReleaseBody_NullBody_ReturnsNull()
        {
            string? result = SHA256Hash.ExtractFromReleaseBody(null!);

            Assert.Null(result);
        }

        [Fact]
        public void ExtractFromReleaseBody_EmptyBody_ReturnsNull()
        {
            string? result = SHA256Hash.ExtractFromReleaseBody("");

            Assert.Null(result);
        }
    }
}
