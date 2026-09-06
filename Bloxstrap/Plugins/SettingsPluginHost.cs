using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;

namespace Bloxstrap.Plugins
{
    public class SettingsPluginHost : IPluginHost
    {
        private readonly INavigationWindow? _navigationWindow;

        public Logger Logger => App.Logger;
        public string BaseDirectory => Paths.Base;
        public string PluginDirectory => Path.Combine(Paths.Base, "Plugins");

        public SettingsPluginHost(INavigationWindow? navigationWindow)
        {
            _navigationWindow = navigationWindow;
        }

        public void RegisterNavigationItem(string content, string icon, string tag, Type pageType)
        {
            if (_navigationWindow is null)
                return;

            var navigation = _navigationWindow.GetNavigation();

            if (navigation is NavigationFluent navFluent)
            {
                var existingItem = navFluent.Items
                    .OfType<NavigationItem>()
                    .FirstOrDefault(x => x.Tag?.ToString() == tag);

                if (existingItem is not null)
                {
                    existingItem.Visibility = System.Windows.Visibility.Visible;
                    return;
                }

                var newItem = new NavigationItem
                {
                    Content = content,
                    Icon = (Wpf.Ui.Common.SymbolRegular)Enum.Parse(typeof(Wpf.Ui.Common.SymbolRegular), icon),
                    PageType = pageType,
                    Tag = tag
                };

                navFluent.Items.Insert(navFluent.Items.Count - 1, newItem);
            }
        }

        public void UnregisterNavigationItem(string tag)
        {
            if (_navigationWindow is null)
                return;

            var navigation = _navigationWindow.GetNavigation();

            if (navigation is NavigationFluent navFluent)
            {
                var item = navFluent.Items
                    .OfType<NavigationItem>()
                    .FirstOrDefault(x => x.Tag?.ToString() == tag);

                if (item is not null)
                    item.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        public PluginCapabilityResult RequestCapability(string pluginId, string capability)
        {
            var manifest = App.PluginManager?.GetManifest(pluginId);
            if (manifest is null)
                return PluginCapabilityResult.Deny("Plugin not found");

            if (!manifest.Permissions.Contains(capability))
                return PluginCapabilityResult.Deny($"Plugin does not declare permission: {capability}");

            return capability switch
            {
                PluginCapabilities.ProcessManagement => CheckProcessManagement(),
                PluginCapabilities.ScreenCapture => CheckScreenCapture(),
                PluginCapabilities.Filesystem => CheckFilesystem(pluginId),
                PluginCapabilities.CredentialStorage => CheckCredentialStorage(),
                PluginCapabilities.CookieAccess => CheckCookieAccess(),
                _ => PluginCapabilityResult.Deny($"Unknown capability: {capability}")
            };
        }

        public bool HasCapability(string pluginId, string capability)
        {
            return RequestCapability(pluginId, capability).Granted;
        }

        private PluginCapabilityResult CheckProcessManagement()
        {
            return PluginCapabilityResult.Allow();
        }

        private PluginCapabilityResult CheckScreenCapture()
        {
            return PluginCapabilityResult.Allow();
        }

        private PluginCapabilityResult CheckFilesystem(string pluginId)
        {
            return PluginCapabilityResult.Allow();
        }

        private PluginCapabilityResult CheckCredentialStorage()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
                return PluginCapabilityResult.Deny("Cookie access is disabled in Rainstrap settings");

            return PluginCapabilityResult.Allow();
        }

        private PluginCapabilityResult CheckCookieAccess()
        {
            if (!App.Settings.Prop.AllowCookieAccess)
                return PluginCapabilityResult.Deny("Cookie access is disabled in Rainstrap settings");

            return PluginCapabilityResult.Allow();
        }
    }
}
