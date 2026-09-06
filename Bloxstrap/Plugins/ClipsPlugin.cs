using System.Diagnostics;
using Bloxstrap.Integrations.Clips;

namespace Bloxstrap.Plugins
{
    public class ClipsPlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new()
        {
            Id = "rainstrap.clips",
            Name = "Clips",
            Version = "1.0.0",
            ApiVersion = "1.0",
            Author = "Rainstrap",
            Description = "Capture Roblox gameplay clips using a rolling replay buffer.",
            IsOfficial = true,
            Verified = true,
            Permissions = new() { "screenCapture", "filesystem" }
        };

        private IPluginHost? _host;
        private ClipsManager? _manager;

        public ClipsManager? Manager => _manager;

        public void Initialize(IPluginHost host)
        {
            const string LOG_IDENT = "ClipsPlugin::Initialize";

            _host = host;

            host.RegisterNavigationItem("Clips", "VideoClip24", "clips", typeof(UI.Elements.Settings.Pages.ClipsPage));

            _manager = new ClipsManager
            {
                BufferDurationSeconds = App.Settings.Prop.ClipsBufferDurationSeconds,
                CaptureFps = App.Settings.Prop.ClipsCaptureFps,
                OutputFolder = App.Settings.Prop.ClipsOutputFolder,
                ClipHotkey = App.Settings.Prop.ClipsHotkeyVirtualKey
            };

            host.Logger.WriteLine(LOG_IDENT, "ClipsManager created");

            if (App.Settings.Prop.ClipsEnabled)
                StartCapturing();

            host.Logger.WriteLine(LOG_IDENT, "Initialized");
        }

        public void OnShutdown()
        {
            const string LOG_IDENT = "ClipsPlugin::OnShutdown";

            _host?.UnregisterNavigationItem("clips");
            StopCapturing();
            _manager?.Dispose();
            _manager = null;

            _host?.Logger.WriteLine(LOG_IDENT, "Shut down");
        }

        public void Dispose()
        {
        }

        public void StartCapturing()
        {
            const string LOG_IDENT = "ClipsPlugin::StartCapturing";

            if (_manager is null || _host is null)
                return;

            if (_manager.Status != ClipsManager.ClipsStatus.Idle)
                return;

            var screenCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.ScreenCapture);
            if (!screenCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Screen capture denied: {screenCap.DenialReason}");
                return;
            }

            var fsCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.Filesystem);
            if (!fsCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Filesystem access denied: {fsCap.DenialReason}");
                return;
            }

            try
            {
                var robloxProcesses = Process.GetProcessesByName("RobloxPlayerBeta");
                if (robloxProcesses.Length == 0)
                {
                    _host.Logger.WriteLine(LOG_IDENT, "Roblox not running, will capture when started");
                    return;
                }

                var robloxProcess = robloxProcesses[0];
                var windowHandle = robloxProcess.MainWindowHandle;

                if (windowHandle == IntPtr.Zero)
                {
                    _host.Logger.WriteLine(LOG_IDENT, "Roblox running but no visible window");
                    return;
                }

                _manager.Initialize(windowHandle, robloxProcess.Id);
                _manager.StartCapturing();
            }
            catch (Exception ex)
            {
                _host.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void StopCapturing()
        {
            _manager?.StopCapturing();
        }

        public async Task<bool> SaveClip()
        {
            const string LOG_IDENT = "ClipsPlugin::SaveClip";

            if (_manager is null || _host is null)
                return false;

            var fsCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.Filesystem);
            if (!fsCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Filesystem access denied: {fsCap.DenialReason}");
                return false;
            }

            await _manager.SaveClip();
            return true;
        }

        public void OpenClipsFolder()
        {
            const string LOG_IDENT = "ClipsPlugin::OpenClipsFolder";

            if (_manager is null || _host is null)
                return;

            var fsCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.Filesystem);
            if (!fsCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Filesystem access denied: {fsCap.DenialReason}");
                return;
            }

            _manager.OpenClipsFolder();
        }
    }
}
