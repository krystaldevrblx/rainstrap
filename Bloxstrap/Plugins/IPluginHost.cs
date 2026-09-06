namespace Bloxstrap.Plugins
{
    public interface IPluginHost
    {
        Logger Logger { get; }

        string BaseDirectory { get; }

        string PluginDirectory { get; }

        void RegisterNavigationItem(string content, string icon, string tag, Type pageType);

        void UnregisterNavigationItem(string tag);

        PluginCapabilityResult RequestCapability(string pluginId, string capability);

        bool HasCapability(string pluginId, string capability);
    }

    public static class PluginCapabilities
    {
        public const string ProcessManagement = "processManagement";
        public const string ScreenCapture = "screenCapture";
        public const string Filesystem = "filesystem";
        public const string CredentialStorage = "credentialStorage";
        public const string CookieAccess = "cookieAccess";
    }

    public class PluginCapabilityResult
    {
        public bool Granted { get; }
        public string? DenialReason { get; }

        private PluginCapabilityResult(bool granted, string? denialReason)
        {
            Granted = granted;
            DenialReason = denialReason;
        }

        public static PluginCapabilityResult Allow() => new(true, null);
        public static PluginCapabilityResult Deny(string reason) => new(false, reason);
    }
}
