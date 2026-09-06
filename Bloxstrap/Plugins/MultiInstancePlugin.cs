using System.Windows;

namespace Bloxstrap.Plugins
{
    public class MultiInstancePlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new()
        {
            Id = "rainstrap.multiinstance",
            Name = "Multi-Instance",
            Version = "1.0.0",
            ApiVersion = "1.0",
            Author = "Rainstrap",
            Description = "Run multiple Roblox instances at the same time - directly through Rainstrap, no third-party tools.",
            IsOfficial = true,
            Verified = true,
            Permissions = new() { "processManagement" }
        };

        private IPluginHost? _host;

        public bool IsMultiInstanceActive { get; private set; }

        public void Initialize(IPluginHost host)
        {
            const string LOG_IDENT = "MultiInstancePlugin::Initialize";

            _host = host;

            var pmCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.ProcessManagement);
            if (!pmCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Process management denied: {pmCap.DenialReason}");
                return;
            }

            IsMultiInstanceActive = true;

            host.RegisterNavigationItem("Multi-Instance", "WindowMultiple20", "multiinstance", typeof(UI.Elements.Settings.Pages.MultiInstancePage));

            host.Logger.WriteLine(LOG_IDENT, "Initialized");
        }

        public void OnShutdown()
        {
            const string LOG_IDENT = "MultiInstancePlugin::OnShutdown";

            _host?.UnregisterNavigationItem("multiinstance");

            IsMultiInstanceActive = false;

            _host?.Logger.WriteLine(LOG_IDENT, "Shut down");
        }

        public void Dispose()
        {
        }

        public bool CheckProcessManagement()
        {
            if (_host is null)
                return false;

            var cap = _host.RequestCapability(Manifest.Id, PluginCapabilities.ProcessManagement);
            if (!cap.Granted)
            {
                _host.Logger.WriteLine("MultiInstancePlugin", $"Process management denied: {cap.DenialReason}");
                return false;
            }

            return true;
        }

        public void SetMultiInstanceLaunching(bool enabled)
        {
            const string LOG_IDENT = "MultiInstancePlugin::SetMultiInstanceLaunching";

            if (_host is null)
                return;

            if (!CheckProcessManagement())
                return;

            App.Settings.Prop.MultiInstanceLaunching = enabled;
            App.Settings.Save();

            _host.Logger.WriteLine(LOG_IDENT, $"MultiInstanceLaunching set to {enabled}");
        }

        public bool TryLaunchInstance()
        {
            const string LOG_IDENT = "MultiInstancePlugin::TryLaunchInstance";

            if (_host is null)
                return false;

            if (!CheckProcessManagement())
                return false;

            if (!IsMultiInstanceActive)
                return false;

            if (!App.Settings.Prop.MultiInstanceLaunching)
            {
                if (Utilities.IsRobloxRunning())
                {
                    var choice = Frontend.ShowMessageBox(
                        Strings.MultiInstance_ConfirmParallel,
                        MessageBoxImage.Warning,
                        MessageBoxButton.YesNo,
                        MessageBoxResult.No
                    );

                    if (choice != MessageBoxResult.Yes)
                        return false;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = "-player",
                    UseShellExecute = false
                });

                _host.Logger.WriteLine(LOG_IDENT, "Launched additional Roblox instance");
                return true;
            }
            catch (Exception ex)
            {
                _host.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox($"{Strings.Accounts_LaunchFailed}\n{ex.Message}", MessageBoxImage.Error);
                return false;
            }
        }
    }
}
