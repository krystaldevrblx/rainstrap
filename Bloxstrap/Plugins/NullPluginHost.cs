namespace Bloxstrap.Plugins
{
    /// <summary>
    /// A minimal plugin host used during early startup when the Settings window
    /// does not yet exist. Provides capability checks and logging without requiring
    /// an INavigationWindow. Navigation registration is a no-op.
    /// </summary>
    public class NullPluginHost : IPluginHost
    {
        public Logger Logger => App.Logger;
        public string BaseDirectory => Paths.Base;
        public string PluginDirectory => Path.Combine(Paths.Base, "Plugins");

        public void RegisterNavigationItem(string content, string icon, string tag, Type pageType)
        {
            // No-op: navigation will be registered when the Settings window opens.
        }

        public void UnregisterNavigationItem(string tag)
        {
            // No-op.
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
                PluginCapabilities.ProcessManagement => PluginCapabilityResult.Allow(),
                PluginCapabilities.ScreenCapture => PluginCapabilityResult.Allow(),
                PluginCapabilities.Filesystem => PluginCapabilityResult.Allow(),
                PluginCapabilities.CredentialStorage => App.Settings.Prop.AllowCookieAccess
                    ? PluginCapabilityResult.Allow()
                    : PluginCapabilityResult.Deny("Cookie access is disabled in Rainstrap settings"),
                PluginCapabilities.CookieAccess => App.Settings.Prop.AllowCookieAccess
                    ? PluginCapabilityResult.Allow()
                    : PluginCapabilityResult.Deny("Cookie access is disabled in Rainstrap settings"),
                _ => PluginCapabilityResult.Deny($"Unknown capability: {capability}")
            };
        }

        public bool HasCapability(string pluginId, string capability)
        {
            return RequestCapability(pluginId, capability).Granted;
        }
    }
}
