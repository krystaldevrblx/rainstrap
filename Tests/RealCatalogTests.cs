using System.Text.Json;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Tests for the actual plugin-catalog/plugins.json file in the repository.
    /// </summary>
    public class RealCatalogTests
    {
        private readonly string _catalogPath;

        public RealCatalogTests()
        {
            // Walk up from the test output directory to find the repo root
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir is not null && !File.Exists(Path.Combine(dir, "plugin-catalog", "plugins.json")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            _catalogPath = Path.Combine(dir ?? "", "plugin-catalog", "plugins.json");
        }

        [Fact]
        public void CatalogFile_Exists()
        {
            Assert.True(File.Exists(_catalogPath), $"Catalog file not found at: {_catalogPath}");
        }

        [Fact]
        public void CatalogFile_IsValidJson()
        {
            string json = File.ReadAllText(_catalogPath);

            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json);

            Assert.NotNull(catalog);
        }

        [Fact]
        public void CatalogFile_ContainsAllBuiltinPlugins()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            Assert.Contains(catalog.Plugins, p => p.Id == "rainstrap.multiinstance" && p.Builtin);
            Assert.Contains(catalog.Plugins, p => p.Id == "rainstrap.clips" && p.Builtin);
            Assert.Contains(catalog.Plugins, p => p.Id == "rainstrap.accountmanager" && p.Builtin);
        }

        [Fact]
        public void CatalogFile_BuiltinPluginsHaveNoPackageUrl()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            foreach (var entry in catalog.Plugins.Where(p => p.Builtin))
            {
                Assert.True(string.IsNullOrEmpty(entry.PackageUrl),
                    $"Builtin plugin {entry.Id} should not have a packageUrl");
                Assert.True(string.IsNullOrEmpty(entry.Sha256),
                    $"Builtin plugin {entry.Id} should not have a sha256");
            }
        }

        [Fact]
        public void CatalogFile_AllEntriesHaveValidApiVersion()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            foreach (var entry in catalog.Plugins)
            {
                Assert.False(string.IsNullOrEmpty(entry.ApiVersion),
                    $"Plugin {entry.Id} is missing apiVersion");
                Assert.True(Version.TryParse(entry.ApiVersion, out _),
                    $"Plugin {entry.Id} has invalid apiVersion: {entry.ApiVersion}");
            }
        }

        [Fact]
        public void CatalogFile_AllEntriesHaveValidId()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            foreach (var entry in catalog.Plugins)
            {
                Assert.False(string.IsNullOrEmpty(entry.Id),
                    "Plugin entry is missing id");
                Assert.Matches(@"^[a-zA-Z0-9._-]+$", entry.Id);
            }
        }

        [Fact]
        public void CatalogFile_AllEntriesHaveNameAndDescription()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            foreach (var entry in catalog.Plugins)
            {
                Assert.False(string.IsNullOrEmpty(entry.Name),
                    $"Plugin {entry.Id} is missing name");
                Assert.False(string.IsNullOrEmpty(entry.Description),
                    $"Plugin {entry.Id} is missing description");
            }
        }

        [Fact]
        public void CatalogFile_SchemaVersionIsAtLeast1()
        {
            string json = File.ReadAllText(_catalogPath);
            var catalog = JsonSerializer.Deserialize<PluginCatalog>(json)!;

            Assert.True(catalog.SchemaVersion >= 1);
        }
    }
}
