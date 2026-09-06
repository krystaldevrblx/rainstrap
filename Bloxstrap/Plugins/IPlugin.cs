namespace Bloxstrap.Plugins
{
    public interface IPlugin : IDisposable
    {
        PluginManifest Manifest { get; }

        void Initialize(IPluginHost host);

        void OnShutdown();
    }
}
