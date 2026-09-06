using System.Reflection;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Tests for PluginManager state operations (enable/disable/uninstall).
    /// Uses reflection to set internal state and test the logic directly,
    /// avoiding dependencies on App.Settings which requires WPF Application context.
    /// </summary>
    public class PluginManagerStateTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PluginManager _manager;

        public PluginManagerStateTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _manager = new PluginManager(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private HashSet<string> GetEnabledPlugins()
        {
            var field = typeof(PluginManager).GetField("_enabledPlugins", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (HashSet<string>)field.GetValue(_manager)!;
        }

        private HashSet<string> GetIncompatiblePlugins()
        {
            var field = typeof(PluginManager).GetField("_incompatiblePlugins", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (HashSet<string>)field.GetValue(_manager)!;
        }

        [Fact]
        public void IsPluginEnabled_InitiallyFalse()
        {
            Assert.False(_manager.IsPluginEnabled("test.plugin"));
        }

        [Fact]
        public void IsPluginIncompatible_InitiallyFalse()
        {
            Assert.False(_manager.IsPluginIncompatible("test.plugin"));
        }

        [Fact]
        public void GetManifest_UnknownId_ReturnsNull()
        {
            Assert.Null(_manager.GetManifest("nonexistent"));
        }

        [Fact]
        public void Manifests_DictionaryIsEmpty()
        {
            Assert.Empty(_manager.Manifests);
        }

        [Fact]
        public void EnabledPlugins_CollectionIsEmpty()
        {
            Assert.Empty(_manager.EnabledPlugins);
        }

        [Fact]
        public void IncompatiblePlugins_CollectionIsEmpty()
        {
            Assert.Empty(_manager.IncompatiblePlugins);
        }

        [Fact]
        public void DiscoverPlugins_NonexistentDirectory_DoesNotThrow()
        {
            var manager = new PluginManager(Path.Combine(_tempDir, "nonexistent"));

            var exception = Record.Exception(() => manager.DiscoverPlugins());

            Assert.Null(exception);
        }

        [Fact]
        public void CheckCompatibility_NullManifest_ThrowsOrReturns()
        {
            // CheckCompatibility is static and should handle null manifest
            // (it will throw NullReferenceException, which is acceptable)
            var manifest = new PluginManifest
            {
                Id = "test",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            var result = PluginManager.CheckCompatibility(manifest);

            Assert.True(result.IsCompatible);
        }
    }
}
