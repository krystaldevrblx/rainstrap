using System.Text.Json;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    public class PluginCatalogTests
    {
        private const string ValidCatalogJson = @"{
            ""schemaVersion"": 1,
            ""plugins"": [
                {
                    ""id"": ""rainstrap.multiinstance"",
                    ""name"": ""Multi-Instance"",
                    ""version"": ""1.0.0"",
                    ""author"": ""Rainstrap"",
                    ""description"": ""Run multiple Roblox instances."",
                    ""apiVersion"": ""1.0"",
                    ""permissions"": [""processManagement""],
                    ""official"": true,
                    ""verified"": true,
                    ""builtin"": true
                },
                {
                    ""id"": ""rainstrap.clips"",
                    ""name"": ""Clips"",
                    ""version"": ""1.0.0"",
                    ""author"": ""Rainstrap"",
                    ""description"": ""Capture gameplay clips."",
                    ""apiVersion"": ""1.0"",
                    ""permissions"": [""screenCapture"", ""filesystem""],
                    ""official"": true,
                    ""verified"": true,
                    ""builtin"": true
                },
                {
                    ""id"": ""rainstrap.accountmanager"",
                    ""name"": ""Account Manager"",
                    ""version"": ""1.0.0"",
                    ""author"": ""Rainstrap"",
                    ""description"": ""Manage multiple accounts."",
                    ""apiVersion"": ""1.0"",
                    ""permissions"": [""credentialStorage"", ""cookieAccess""],
                    ""official"": true,
                    ""verified"": true,
                    ""builtin"": true
                }
            ]
        }";

        [Fact]
        public void Deserialize_ValidCatalog_ReturnsCorrectSchemaVersion()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);
            Assert.Equal(1, catalog!.SchemaVersion);
        }

        [Fact]
        public void Deserialize_ValidCatalog_ContainsThreePlugins()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);
            Assert.Equal(3, catalog!.Plugins.Count);
        }

        [Fact]
        public void Deserialize_ValidCatalog_BuiltinPluginsAreMarkedBuiltin()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);
            foreach (var entry in catalog!.Plugins)
            {
                Assert.True(entry.Builtin);
            }
        }

        [Fact]
        public void Deserialize_ValidCatalog_PluginIdsMatch()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);
            Assert.Contains(catalog!.Plugins, p => p.Id == "rainstrap.multiinstance");
            Assert.Contains(catalog.Plugins, p => p.Id == "rainstrap.clips");
            Assert.Contains(catalog.Plugins, p => p.Id == "rainstrap.accountmanager");
        }

        [Fact]
        public void Deserialize_ValidCatalog_PluginApiVersionsAre10()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);
            foreach (var entry in catalog!.Plugins)
            {
                Assert.Equal("1.0", entry.ApiVersion);
            }
        }

        [Fact]
        public void Deserialize_ValidCatalog_PluginPermissionsAreCorrect()
        {
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(ValidCatalogJson);

            Assert.NotNull(catalog);

            var mi = catalog!.Plugins.First(p => p.Id == "rainstrap.multiinstance");
            Assert.Single(mi.Permissions);
            Assert.Contains("processManagement", mi.Permissions);

            var clips = catalog.Plugins.First(p => p.Id == "rainstrap.clips");
            Assert.Equal(2, clips.Permissions.Count);
            Assert.Contains("screenCapture", clips.Permissions);
            Assert.Contains("filesystem", clips.Permissions);

            var am = catalog.Plugins.First(p => p.Id == "rainstrap.accountmanager");
            Assert.Equal(2, am.Permissions.Count);
            Assert.Contains("credentialStorage", am.Permissions);
            Assert.Contains("cookieAccess", am.Permissions);
        }

        [Fact]
        public void Deserialize_EmptyCatalog_DefaultsAreCorrect()
        {
            string json = @"{""schemaVersion"": 1, ""plugins"": []}";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            Assert.Empty(catalog!.Plugins);
        }

        [Fact]
        public void Deserialize_MalformedJson_ThrowsOrReturnsNull()
        {
            string json = @"{""not_valid"":";

            var exception = Record.Exception(() => JsonSerializer.Deserialize<PluginCatalog>(json));

            Assert.NotNull(exception);
        }

        [Fact]
        public void Deserialize_MissingPlugins_DefaultsToEmptyList()
        {
            string json = @"{""schemaVersion"": 1}";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            Assert.NotNull(catalog!.Plugins);
            Assert.Empty(catalog.Plugins);
        }

        [Fact]
        public void Deserialize_BuiltinFalse_EntryIsNotBuiltin()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""plugins"": [{
                    ""id"": ""external.plugin"",
                    ""name"": ""External"",
                    ""version"": ""1.0.0"",
                    ""apiVersion"": ""1.0"",
                    ""builtin"": false,
                    ""packageUrl"": ""https://example.com/pkg.rspkg"",
                    ""sha256"": ""abc123""
                }]
            }";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            Assert.Single(catalog!.Plugins);
            Assert.False(catalog.Plugins[0].Builtin);
            Assert.Equal("https://example.com/pkg.rspkg", catalog.Plugins[0].PackageUrl);
            Assert.Equal("abc123", catalog.Plugins[0].Sha256);
        }

        [Fact]
        public void Deserialize_ExternalPlugin_RequiresPackageUrlAndSha256()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""plugins"": [{
                    ""id"": ""external.plugin"",
                    ""name"": ""External"",
                    ""version"": ""1.0.0"",
                    ""apiVersion"": ""1.0"",
                    ""builtin"": false,
                    ""packageUrl"": ""https://example.com/pkg.rspkg"",
                    ""sha256"": ""abc123""
                }]
            }";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            var entry = catalog!.Plugins[0];
            Assert.False(entry.Builtin);
            Assert.False(string.IsNullOrEmpty(entry.PackageUrl));
            Assert.False(string.IsNullOrEmpty(entry.Sha256));
        }

        [Fact]
        public void Deserialize_BuiltinPlugin_OmitsPackageUrlAndSha256()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""plugins"": [{
                    ""id"": ""rainstrap.multiinstance"",
                    ""name"": ""Multi-Instance"",
                    ""version"": ""1.0.0"",
                    ""apiVersion"": ""1.0"",
                    ""builtin"": true
                }]
            }";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            var entry = catalog!.Plugins[0];
            Assert.True(entry.Builtin);
            Assert.Equal("", entry.PackageUrl);
            Assert.Equal("", entry.Sha256);
        }

        [Fact]
        public void Deserialize_MalformedEntry_DoesNotCrashCatalog()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""plugins"": [
                    {""id"": ""good.plugin"", ""name"": ""Good"", ""version"": ""1.0.0"", ""apiVersion"": ""1.0""},
                    {""bad"": true},
                    {""id"": ""also.good"", ""name"": ""Also Good"", ""version"": ""2.0.0"", ""apiVersion"": ""1.0""}
                ]
            }";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            Assert.Equal(3, catalog!.Plugins.Count);
        }

        [Fact]
        public void Deserialize_SchemaVersionZero_IsParsed()
        {
            string json = @"{""schemaVersion"": 0, ""plugins"": []}";

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
            Assert.Equal(0, catalog!.SchemaVersion);
        }

        [Fact]
        public void PluginCatalogEntry_BuiltinDefault_IsFalse()
        {
            var entry = new PluginCatalogEntry();
            Assert.False(entry.Builtin);
        }
    }
}
