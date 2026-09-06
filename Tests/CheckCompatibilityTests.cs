using Bloxstrap;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    public class CheckCompatibilityTests
    {
        private readonly string _originalVersion;

        public CheckCompatibilityTests()
        {
            _originalVersion = App.Version;
        }

        [Fact]
        public void Compatible_SameApiVersion_ReturnsCompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.True(result.IsCompatible);
        }

        [Fact]
        public void Compatible_DifferentMinorVersion_ReturnsCompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "1.5"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.True(result.IsCompatible);
        }

        [Fact]
        public void Incompatible_DifferentMajorVersion_ReturnsIncompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "2.0"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.False(result.IsCompatible);
            Assert.Contains("API", result.Reason);
        }

        [Fact]
        public void Incompatible_EmptyApiVersion_ReturnsIncompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = ""
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.False(result.IsCompatible);
            Assert.Contains("Missing", result.Reason);
        }

        [Fact]
        public void Incompatible_MalformedApiVersion_ReturnsIncompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "not-a-version"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.False(result.IsCompatible);
            Assert.Contains("Malformed", result.Reason);
        }

        [Fact]
        public void Compatible_WithMinRainstrapVersion_MeetsRequirement()
        {
            App.Version = "2.0.0";
            try
            {
                var manifest = new PluginManifest
                {
                    Id = "test.plugin",
                    Name = "Test",
                    Version = "1.0.0",
                    ApiVersion = "1.0",
                    MinRainstrapVersion = "1.0.0"
                };

                var result = PluginManager.CheckCompatibility(manifest);

                Assert.True(result.IsCompatible);
            }
            finally
            {
                App.Version = _originalVersion;
            }
        }

        [Fact]
        public void Incompatible_WithMinRainstrapVersion_DoesNotMeetRequirement()
        {
            App.Version = "1.0.0";
            try
            {
                var manifest = new PluginManifest
                {
                    Id = "test.plugin",
                    Name = "Test",
                    Version = "1.0.0",
                    ApiVersion = "1.0",
                    MinRainstrapVersion = "2.0.0"
                };

                var result = PluginManager.CheckCompatibility(manifest);

                Assert.False(result.IsCompatible);
                Assert.Contains("Requires", result.Reason);
            }
            finally
            {
                App.Version = _originalVersion;
            }
        }

        [Fact]
        public void Incompatible_MalformedMinVersion_ReturnsIncompatible()
        {
            var manifest = new PluginManifest
            {
                Id = "test.plugin",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "1.0",
                MinRainstrapVersion = "not-a-version"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.False(result.IsCompatible);
            Assert.Contains("Malformed", result.Reason);
        }
    }
}
