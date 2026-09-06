using Bloxstrap;
using Bloxstrap.Plugins;

namespace Rainstrap.Tests
{
    public class NullPluginHostTests
    {
        private readonly NullPluginHost _host = new();

        [Fact]
        public void Logger_ReturnsAppLogger()
        {
            Assert.Same(App.Logger, _host.Logger);
        }

        [Fact]
        public void RegisterNavigationItem_DoesNotThrow()
        {
            var exception = Record.Exception(() =>
                _host.RegisterNavigationItem("Test", "PuzzlePiece24", "test", typeof(object)));

            Assert.Null(exception);
        }

        [Fact]
        public void UnregisterNavigationItem_DoesNotThrow()
        {
            var exception = Record.Exception(() => _host.UnregisterNavigationItem("test"));

            Assert.Null(exception);
        }

        [Fact]
        public void RequestCapability_PluginManagerNull_ReturnsDenied()
        {
            // When PluginManager is null, GetManifest returns null → denied
            var result = _host.RequestCapability("test.plugin", PluginCapabilities.ProcessManagement);

            Assert.False(result.Granted);
            Assert.Contains("not found", result.DenialReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RequestCapability_PluginNotInManifests_ReturnsDenied()
        {
            // Set up a temporary PluginManager so GetManifest can be called
            string tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var originalPm = App.PluginManager;
                try
                {
                    App.PluginManager = new PluginManager(tempDir);

                    var result = _host.RequestCapability("nonexistent.plugin", PluginCapabilities.ProcessManagement);

                    Assert.False(result.Granted);
                    Assert.Contains("not found", result.DenialReason, StringComparison.OrdinalIgnoreCase);
                }
                finally
                {
                    App.PluginManager = originalPm;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RequestCapability_UnknownCapability_ReturnsDenied()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var originalPm = App.PluginManager;
                try
                {
                    App.PluginManager = new PluginManager(tempDir);
                    App.PluginManager.Manifests["test.plugin"] = new PluginManifest
                    {
                        Id = "test.plugin",
                        Name = "Test",
                        Version = "1.0.0",
                        ApiVersion = "1.0",
                        Permissions = new() { "processManagement" }
                    };

                    var result = _host.RequestCapability("test.plugin", "unknownCapability");

                    Assert.False(result.Granted);
                    Assert.Contains("does not declare", result.DenialReason);
                }
                finally
                {
                    App.PluginManager = originalPm;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RequestCapability_ProcessManagement_ReturnsAllowed()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var originalPm = App.PluginManager;
                try
                {
                    App.PluginManager = new PluginManager(tempDir);
                    App.PluginManager.Manifests["test.plugin"] = new PluginManifest
                    {
                        Id = "test.plugin",
                        Name = "Test",
                        Version = "1.0.0",
                        ApiVersion = "1.0",
                        Permissions = new() { "processManagement" }
                    };

                    var result = _host.RequestCapability("test.plugin", PluginCapabilities.ProcessManagement);

                    Assert.True(result.Granted);
                }
                finally
                {
                    App.PluginManager = originalPm;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void RequestCapability_UndeclaredPermission_ReturnsDenied()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var originalPm = App.PluginManager;
                try
                {
                    App.PluginManager = new PluginManager(tempDir);
                    App.PluginManager.Manifests["test.plugin"] = new PluginManifest
                    {
                        Id = "test.plugin",
                        Name = "Test",
                        Version = "1.0.0",
                        ApiVersion = "1.0",
                        Permissions = new() { "processManagement" }
                    };

                    // screenCapture is NOT declared in permissions
                    var result = _host.RequestCapability("test.plugin", PluginCapabilities.ScreenCapture);

                    Assert.False(result.Granted);
                    Assert.Contains("does not declare", result.DenialReason);
                }
                finally
                {
                    App.PluginManager = originalPm;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void HasCapability_DelegatesToRequestCapability()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            try
            {
                var originalPm = App.PluginManager;
                try
                {
                    App.PluginManager = new PluginManager(tempDir);
                    App.PluginManager.Manifests["test.plugin"] = new PluginManifest
                    {
                        Id = "test.plugin",
                        Name = "Test",
                        Version = "1.0.0",
                        ApiVersion = "1.0",
                        Permissions = new() { "processManagement" }
                    };

                    bool hasIt = _host.HasCapability("test.plugin", PluginCapabilities.ProcessManagement);

                    Assert.True(hasIt);
                }
                finally
                {
                    App.PluginManager = originalPm;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
