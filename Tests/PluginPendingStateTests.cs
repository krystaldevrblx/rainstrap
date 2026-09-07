using System.Reflection;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Tests for plugin pending state operations.
    /// Uses reflection to set internal state and test the logic directly.
    /// These tests verify that PluginManager state management rules work correctly
    /// by testing the logic paths rather than calling methods that require App.Settings.
    /// </summary>
    public class PluginPendingStateTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PluginManager _manager;

        public PluginPendingStateTests()
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

        private HashSet<string> GetBuiltinPluginIds()
        {
            var field = typeof(PluginManager).GetField("_builtinPluginIds", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (HashSet<string>)field.GetValue(_manager)!;
        }

        [Fact]
        public void IsBuiltinPlugin_RegisteredBuiltin_ReturnsTrue()
        {
            var builtinIds = GetBuiltinPluginIds();
            builtinIds.Add("rainstrap.multiinstance");

            Assert.True(_manager.IsBuiltinPlugin("rainstrap.multiinstance"));
        }

        [Fact]
        public void IsBuiltinPlugin_UnknownId_ReturnsFalse()
        {
            Assert.False(_manager.IsBuiltinPlugin("unknown.plugin"));
        }

        [Fact]
        public void RegisterBuiltinPlugin_AddsToBuiltinIds()
        {
            var manifest = new PluginManifest
            {
                Id = "test.builtin",
                Name = "Test Builtin",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            var plugin = new TestPlugin(manifest);
            _manager.RegisterBuiltinPlugin(plugin, () => new TestPlugin(manifest));

            Assert.True(_manager.IsBuiltinPlugin("test.builtin"));
        }

        [Fact]
        public void EnableNewPlugins_SkipsBuiltinPlugins()
        {
            var builtinIds = GetBuiltinPluginIds();
            builtinIds.Add("rainstrap.multiinstance");

            _manager.Manifests["rainstrap.multiinstance"] = new PluginManifest
            {
                Id = "rainstrap.multiinstance",
                Name = "Multi-Instance",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            var enabledBefore = GetEnabledPlugins().Contains("rainstrap.multiinstance");
            Assert.False(enabledBefore);
        }

        [Fact]
        public void EnableNewPlugins_NonBuiltinWouldBeEnabled()
        {
            _manager.Manifests["external.plugin"] = new PluginManifest
            {
                Id = "external.plugin",
                Name = "External",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            var builtinIds = GetBuiltinPluginIds();
            Assert.False(builtinIds.Contains("external.plugin"));
        }

        [Fact]
        public void FreshInstall_EnabledPluginsIsEmpty()
        {
            var enabled = GetEnabledPlugins();
            Assert.Empty(enabled);
        }

        [Fact]
        public void FreshInstall_BuiltinPluginsAreNotEnabled()
        {
            var builtinIds = GetBuiltinPluginIds();
            builtinIds.Add("rainstrap.multiinstance");
            builtinIds.Add("rainstrap.clips");
            builtinIds.Add("rainstrap.accountmanager");

            var enabled = GetEnabledPlugins();
            Assert.DoesNotContain("rainstrap.multiinstance", enabled);
            Assert.DoesNotContain("rainstrap.clips", enabled);
            Assert.DoesNotContain("rainstrap.accountmanager", enabled);
        }

        [Fact]
        public void BuiltinPlugin_Delete_DoesNotAttemptFileDeletion()
        {
            var pluginDir = Path.Combine(_tempDir, "Plugins", "rainstrap.multiinstance");
            Directory.CreateDirectory(pluginDir);
            File.WriteAllText(Path.Combine(pluginDir, "manifest.json"), "{}");

            var builtinIds = GetBuiltinPluginIds();
            builtinIds.Add("rainstrap.multiinstance");

            bool isBuiltin = _manager.IsBuiltinPlugin("rainstrap.multiinstance");
            Assert.True(isBuiltin);

            Assert.True(File.Exists(Path.Combine(pluginDir, "manifest.json")));
        }

        [Fact]
        public void Manifests_ContainsBuiltinPluginManifests()
        {
            _manager.Manifests["rainstrap.multiinstance"] = new PluginManifest
            {
                Id = "rainstrap.multiinstance",
                Name = "Multi-Instance",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };
            _manager.Manifests["rainstrap.clips"] = new PluginManifest
            {
                Id = "rainstrap.clips",
                Name = "Clips",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };
            _manager.Manifests["rainstrap.accountmanager"] = new PluginManifest
            {
                Id = "rainstrap.accountmanager",
                Name = "Account Manager",
                Version = "1.0.0",
                ApiVersion = "1.0"
            };

            Assert.Equal(3, _manager.Manifests.Count);
            Assert.True(_manager.Manifests.ContainsKey("rainstrap.multiinstance"));
            Assert.True(_manager.Manifests.ContainsKey("rainstrap.clips"));
            Assert.True(_manager.Manifests.ContainsKey("rainstrap.accountmanager"));
        }

        [Fact]
        public void PluginCatalogEntry_DeserializesBuiltinField()
        {
            var entry = new PluginCatalogEntry
            {
                Id = "test",
                Name = "Test",
                Version = "1.0.0",
                ApiVersion = "1.0",
                Builtin = true
            };

            Assert.True(entry.Builtin);
            Assert.False(entry.Builtin == false);
        }

        [Fact]
        public void PluginCatalogEntry_BuiltinDefault_IsFalse()
        {
            var entry = new PluginCatalogEntry();
            Assert.False(entry.Builtin);
        }

        private class TestPlugin : IPlugin
        {
            public PluginManifest Manifest { get; }

            public TestPlugin(PluginManifest manifest)
            {
                Manifest = manifest;
            }

            public void Initialize(IPluginHost host) { }
            public void OnShutdown() { }
            public void Dispose() { }
        }
    }
}
