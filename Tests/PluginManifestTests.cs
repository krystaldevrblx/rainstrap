using System.Text.Json;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    public class PluginManifestTests
    {
        [Fact]
        public void Deserialize_ValidManifest_ReturnsCorrectValues()
        {
            string json = @"{
                ""id"": ""test.plugin"",
                ""name"": ""Test Plugin"",
                ""version"": ""1.0.0"",
                ""apiVersion"": ""1.0"",
                ""minRainstrapVersion"": ""2.0.0"",
                ""author"": ""Tester"",
                ""description"": ""A test plugin"",
                ""permissions"": [""screenCapture"", ""filesystem""],
                ""verified"": true,
                ""isOfficial"": false
            }";

            var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

            Assert.NotNull(manifest);
            Assert.Equal("test.plugin", manifest!.Id);
            Assert.Equal("Test Plugin", manifest.Name);
            Assert.Equal("1.0.0", manifest.Version);
            Assert.Equal("1.0", manifest.ApiVersion);
            Assert.Equal("2.0.0", manifest.MinRainstrapVersion);
            Assert.Equal("Tester", manifest.Author);
            Assert.Equal("A test plugin", manifest.Description);
            Assert.Equal(2, manifest.Permissions.Count);
            Assert.Contains("screenCapture", manifest.Permissions);
            Assert.Contains("filesystem", manifest.Permissions);
            Assert.True(manifest.Verified);
            Assert.False(manifest.IsOfficial);
        }

        [Fact]
        public void Deserialize_MinimalManifest_DefaultsAreCorrect()
        {
            string json = @"{
                ""id"": ""test.plugin"",
                ""name"": ""Test"",
                ""version"": ""1.0.0"",
                ""apiVersion"": ""1.0""
            }";

            var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

            Assert.NotNull(manifest);
            Assert.Equal("", manifest!.MinRainstrapVersion);
            Assert.Equal("", manifest.Author);
            Assert.Equal("", manifest.Description);
            Assert.NotNull(manifest.Permissions);
            Assert.Empty(manifest.Permissions);
            Assert.False(manifest.Verified);
            Assert.False(manifest.IsOfficial);
        }

        [Fact]
        public void Deserialize_EmptyPermissions_DefaultsToEmptyList()
        {
            string json = @"{
                ""id"": ""test.plugin"",
                ""name"": ""Test"",
                ""version"": ""1.0.0"",
                ""apiVersion"": ""1.0"",
                ""permissions"": []
            }";

            var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

            Assert.NotNull(manifest);
            Assert.NotNull(manifest!.Permissions);
            Assert.Empty(manifest.Permissions);
        }
    }
}
