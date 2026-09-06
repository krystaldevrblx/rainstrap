using System.Text.Json;

namespace Bloxstrap.Plugins
{
    public class PluginManager
    {
        const string LOG_IDENT = "PluginManager";

        private const string SupportedApiVersion = "1.0";

        private readonly Dictionary<string, IPlugin> _plugins = new();
        private readonly Dictionary<string, PluginManifest> _manifests = new();
        private readonly Dictionary<string, Func<IPlugin>> _pluginFactories = new();
        private readonly HashSet<string> _enabledPlugins = new();
        private readonly HashSet<string> _incompatiblePlugins = new();
        private readonly HashSet<string> _initializedPlugins = new();
        private readonly HashSet<string> _builtinPluginIds = new();
        private readonly string _pluginsDirectory;
        private IPluginHost? _host;

        public IReadOnlyDictionary<string, IPlugin> Plugins => _plugins;
        public Dictionary<string, PluginManifest> Manifests => _manifests;
        public IReadOnlyCollection<string> EnabledPlugins => _enabledPlugins;
        public IReadOnlyCollection<string> IncompatiblePlugins => _incompatiblePlugins;
        public PluginCatalogClient CatalogClient { get; private set; } = null!;

        public bool IsPluginEnabled(string pluginId) => _enabledPlugins.Contains(pluginId);
        public bool IsPluginIncompatible(string pluginId) => _incompatiblePlugins.Contains(pluginId);

        public PluginManager(string baseDirectory)
        {
            _pluginsDirectory = Path.Combine(baseDirectory, "Plugins");
            Directory.CreateDirectory(_pluginsDirectory);
            CatalogClient = new PluginCatalogClient(baseDirectory);
        }

        public bool IsPluginInstalled(string pluginId)
        {
            return _manifests.ContainsKey(pluginId);
        }

        public PluginInstaller CreateInstaller()
        {
            return new PluginInstaller(_pluginsDirectory);
        }

        /// <summary>
        /// Applies pending enable/disable/uninstall changes from a previous session.
        /// Called during startup after DiscoverPlugins and LoadPluginStates.
        /// </summary>
        public void ApplyPendingChanges()
        {
            const string LOG_ID = "PluginManager::ApplyPendingChanges";
            var settings = App.Settings.Prop;
            bool changed = false;

            foreach (string pluginId in settings.PendingUninstall.ToList())
            {
                App.Logger.WriteLine(LOG_ID, $"Pending uninstall: {pluginId}");

                string pluginDir = Path.Combine(_pluginsDirectory, pluginId);
                if (Directory.Exists(pluginDir))
                {
                    try
                    {
                        Directory.Delete(pluginDir, recursive: true);
                        App.Logger.WriteLine(LOG_ID, $"Uninstalled plugin directory: {pluginId}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_ID, ex);
                        App.Logger.WriteLine(LOG_ID, $"Failed to uninstall plugin directory: {pluginId}");
                    }
                }

                settings.EnabledPlugins.Remove(pluginId);
                settings.PendingEnable.Remove(pluginId);
                settings.PendingDisable.Remove(pluginId);
                settings.PendingUninstall.Remove(pluginId);
                changed = true;
            }

            foreach (string pluginId in settings.PendingEnable.ToList())
            {
                App.Logger.WriteLine(LOG_ID, $"Pending enable: {pluginId}");

                if (settings.PendingDisable.Contains(pluginId))
                {
                    settings.PendingDisable.Remove(pluginId);
                }

                if (!settings.EnabledPlugins.Contains(pluginId))
                    settings.EnabledPlugins.Add(pluginId);

                settings.PendingEnable.Remove(pluginId);
                changed = true;
            }

            foreach (string pluginId in settings.PendingDisable.ToList())
            {
                App.Logger.WriteLine(LOG_ID, $"Pending disable: {pluginId}");

                settings.EnabledPlugins.Remove(pluginId);
                settings.PendingDisable.Remove(pluginId);
                changed = true;
            }

            if (changed)
                App.Settings.Save();
        }

        /// <summary>
        /// Requests enable of a plugin, which takes effect on next restart.
        /// Cancels any pending disable for this plugin.
        /// </summary>
        public void RequestEnable(string pluginId)
        {
            var settings = App.Settings.Prop;
            settings.PendingDisable.Remove(pluginId);
            settings.PendingEnable.Add(pluginId);
            App.Settings.Save();
        }

        /// <summary>
        /// Requests disable of a plugin, which takes effect on next restart.
        /// Cancels any pending enable for this plugin.
        /// </summary>
        public void RequestDisable(string pluginId)
        {
            var settings = App.Settings.Prop;
            settings.PendingEnable.Remove(pluginId);
            settings.PendingDisable.Add(pluginId);
            App.Settings.Save();
        }

        /// <summary>
        /// Requests uninstall of a plugin, which takes effect on next restart.
        /// </summary>
        public void RequestUninstall(string pluginId)
        {
            var settings = App.Settings.Prop;
            settings.PendingUninstall.Add(pluginId);
            settings.PendingDisable.Remove(pluginId);
            settings.PendingEnable.Remove(pluginId);
            App.Settings.Save();
        }

        /// <summary>
        /// Installs a plugin from a local package file and discovers it.
        /// Returns the installed manifest, or null on failure.
        /// </summary>
        public PluginManifest? InstallPlugin(string packagePath)
        {
            const string LOG_ID = "PluginManager::InstallPlugin";
            var installer = CreateInstaller();
            var manifest = installer.InstallPlugin(packagePath);

            if (manifest is null)
            {
                App.Logger.WriteLine(LOG_ID, $"Failed to install plugin from {packagePath}");
                return null;
            }

            _manifests[manifest.Id] = manifest;
            App.Logger.WriteLine(LOG_ID, $"Installed and discovered plugin: {manifest.Id} v{manifest.Version}");

            return manifest;
        }

        /// <summary>
        /// Updates a plugin from a local package file. Shuts down the plugin if active,
        /// replaces files transactionally with backup/restore, and re-initializes.
        /// Returns true on success.
        /// </summary>
        public bool UpdatePlugin(string pluginId, string packagePath)
        {
            const string LOG_ID = "PluginManager::UpdatePlugin";

            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    plugin.OnShutdown();
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_ID, ex);
                }

                _plugins.Remove(pluginId);
            }

            var installer = CreateInstaller();
            bool success = installer.UpdatePlugin(pluginId, packagePath);

            if (success)
            {
                if (_manifests.TryGetValue(pluginId, out var oldManifest))
                {
                    string manifestPath = Path.Combine(_pluginsDirectory, pluginId, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(manifestPath);
                            var newManifest = JsonSerializer.Deserialize<PluginManifest>(json);
                            if (newManifest is not null)
                                _manifests[pluginId] = newManifest;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteException(LOG_ID, ex);
                        }
                    }
                }

                App.Logger.WriteLine(LOG_ID, $"Updated plugin: {pluginId}");

                if (_enabledPlugins.Contains(pluginId) && _host is not null)
                {
                    try
                    {
                        string pluginDir = Path.Combine(_pluginsDirectory, pluginId);
                        string dllPath = Path.Combine(pluginDir, $"{pluginId}.dll");

                        if (File.Exists(dllPath))
                        {
                            var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                            var pluginType = assembly.GetTypes()
                                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                            if (pluginType is not null)
                            {
                                var newPlugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                                newPlugin.Initialize(_host);
                                _plugins[pluginId] = newPlugin;
                                App.Logger.WriteLine(LOG_ID, $"Re-initialized updated plugin: {pluginId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_ID, ex);
                        App.Logger.WriteLine(LOG_ID, $"Failed to re-initialize updated plugin: {pluginId}");
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// Uninstalls a plugin. Shuts it down if active, removes files, and cleans state.
        /// Returns true on success.
        /// </summary>
        public bool UninstallPlugin(string pluginId)
        {
            const string LOG_ID = "PluginManager::UninstallPlugin";

            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    plugin.OnShutdown();
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_ID, ex);
                }

                _plugins.Remove(pluginId);
            }

            var installer = CreateInstaller();
            bool success = installer.UninstallPlugin(pluginId);

            if (success)
            {
                _manifests.Remove(pluginId);
                _enabledPlugins.Remove(pluginId);
                _incompatiblePlugins.Remove(pluginId);

                var settings = App.Settings.Prop;
                settings.EnabledPlugins.Remove(pluginId);
                settings.PendingEnable.Remove(pluginId);
                settings.PendingDisable.Remove(pluginId);
                settings.PendingUninstall.Remove(pluginId);
                App.Settings.Save();

                App.Logger.WriteLine(LOG_ID, $"Uninstalled plugin: {pluginId}");
            }

            return success;
        }

        /// <summary>
        /// Initializes builtin plugins with a minimal NullPluginHost during early startup,
        /// so they are active before the Settings window exists (e.g. during direct Roblox launch).
        /// </summary>
        public void InitializeBuiltinPlugins()
        {
            if (_host is not null)
                return;

            _host = new NullPluginHost();

            InitializeBuiltinPluginsWithHost(_host);
        }

        public void SetHost(IPluginHost host)
        {
            _host = host;

            // Builtin plugins were already initialized via InitializeBuiltinPlugins().
            // We cannot re-call Initialize() on them (it would recreate timers, managers,
            // etc.). Instead, plugins that need the host for navigation will get it when
            // they call RegisterNavigationItem — but since _host is now set, any future
            // capability checks will use the real host.
            //
            // For plugins that stored _host = NullPluginHost, their existing _host reference
            // is fine — capability checks and logging still work. The real host is only
            // needed for navigation, which the Settings window handles separately via
            // UpdatePluginNavigationVisibility.
            //
            // If SetHost is called for the first time (no prior InitializeBuiltinPlugins),
            // initialize all builtin plugins now.
            if (_initializedPlugins.Count > 0)
            {
                App.Logger.WriteLine(LOG_IDENT, "Builtin plugins already initialized; updating host reference only");
                return;
            }

            InitializeBuiltinPluginsWithHost(host);
        }

        private void InitializeBuiltinPluginsWithHost(IPluginHost host)
        {
            foreach (var kvp in _plugins)
            {
                if (_pluginFactories.ContainsKey(kvp.Key) && !_initializedPlugins.Contains(kvp.Key))
                {
                    try
                    {
                        kvp.Value.Initialize(host);
                        _initializedPlugins.Add(kvp.Key);
                        App.Logger.WriteLine(LOG_IDENT, $"Initialized builtin plugin: {kvp.Key}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to initialize builtin plugin: {kvp.Key}");
                    }
                }
            }
        }

        /// <summary>
        /// Re-registers navigation items for enabled builtin plugins using the current host.
        /// Called when the Settings window opens to ensure nav items are visible even if
        /// plugins were initialized before the window existed.
        /// </summary>
        public void RegisterBuiltinNavigationItems()
        {
            if (_host is null)
                return;

            foreach (var kvp in _plugins)
            {
                if (_pluginFactories.ContainsKey(kvp.Key) && _enabledPlugins.Contains(kvp.Key))
                {
                    try
                    {
                        // Each builtin plugin registers its nav item during Initialize().
                        // Since the plugin is already initialized, we call a focused method
                        // to re-register the nav item with the current (real) host.
                        switch (kvp.Value)
                        {
                            case MultiInstancePlugin mi:
                                _host.RegisterNavigationItem("Multi-Instance", "WindowMultiple20", "multiinstance",
                                    typeof(UI.Elements.Settings.Pages.MultiInstancePage));
                                break;
                            case ClipsPlugin cl:
                                _host.RegisterNavigationItem("Clips", "VideoClip24", "clips",
                                    typeof(UI.Elements.Settings.Pages.ClipsPage));
                                break;
                            case AccountManagerPlugin am:
                                _host.RegisterNavigationItem("Accounts", "PeopleList24", "accounts",
                                    typeof(UI.Elements.Settings.Pages.AccountsPage));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to register navigation for builtin plugin: {kvp.Key}");
                    }
                }
            }
        }

        public void DiscoverPlugins()
        {
            App.Logger.WriteLine(LOG_IDENT, "Discovering plugins");

            if (!Directory.Exists(_pluginsDirectory))
                return;

            foreach (string pluginDir in Directory.GetDirectories(_pluginsDirectory))
            {
                string manifestPath = Path.Combine(pluginDir, "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No manifest.json in {pluginDir}");
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

                    if (manifest is null || string.IsNullOrEmpty(manifest.Id))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Invalid manifest in {pluginDir}");
                        continue;
                    }

                    _manifests[manifest.Id] = manifest;
                    App.Logger.WriteLine(LOG_IDENT, $"Discovered plugin: {manifest.Id} v{manifest.Version} by {manifest.Author}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        public void LoadPluginStates()
        {
            var enabledList = App.Settings.Prop.EnabledPlugins;

            foreach (var manifest in _manifests.Values)
            {
                if (enabledList.Contains(manifest.Id))
                    _enabledPlugins.Add(manifest.Id);
            }
        }

        public void EnableNewPlugins()
        {
            var enabledList = App.Settings.Prop.EnabledPlugins;
            bool changed = false;

            foreach (var manifest in _manifests.Values)
            {
                // Builtin optional plugins are registered for Plugin Store visibility
                // but must not be auto-enabled on fresh install. They are only enabled
                // when the user explicitly installs/enables them via the Plugin Store.
                if (_builtinPluginIds.Contains(manifest.Id))
                    continue;

                if (!_enabledPlugins.Contains(manifest.Id))
                {
                    _enabledPlugins.Add(manifest.Id);

                    if (!enabledList.Contains(manifest.Id))
                    {
                        enabledList.Add(manifest.Id);
                        changed = true;
                    }
                }
            }

            if (changed)
                App.Settings.Save();
        }

        public void InitializePlugins()
        {
            if (_host is null)
                throw new InvalidOperationException("PluginHost not set");

            const string LOG_IDENT = "PluginManager::InitializePlugins";

            foreach (var manifest in _manifests.Values)
            {
                if (!_enabledPlugins.Contains(manifest.Id))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping disabled plugin: {manifest.Id}");
                    continue;
                }

                var compat = CheckCompatibility(manifest);
                if (!compat.IsCompatible)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Plugin {manifest.Id} is incompatible: {compat.Reason}");
                    _incompatiblePlugins.Add(manifest.Id);
                    continue;
                }

                try
                {
                    string pluginDir = Path.Combine(_pluginsDirectory, manifest.Id);
                    string dllPath = Path.Combine(pluginDir, $"{manifest.Id}.dll");

                    if (!File.Exists(dllPath))
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Plugin DLL not found: {dllPath}");
                        continue;
                    }

                    var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                    var pluginType = assembly.GetTypes()
                        .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    if (pluginType is null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"No IPlugin implementation found in {dllPath}");
                        continue;
                    }

                    var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                    plugin.Initialize(_host);
                    _plugins[manifest.Id] = plugin;

                    App.Logger.WriteLine(LOG_IDENT, $"Initialized plugin: {manifest.Id}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to initialize plugin: {manifest.Id}");
                }
            }
        }

        public void RegisterBuiltinPlugin(IPlugin plugin, Func<IPlugin>? factory = null)
        {
            const string LOG_IDENT = "PluginManager::RegisterBuiltinPlugin";

            string pluginId = plugin.Manifest.Id;

            _builtinPluginIds.Add(pluginId);

            if (factory is not null)
                _pluginFactories[pluginId] = factory;

            if (!_enabledPlugins.Contains(pluginId))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Builtin plugin {pluginId} is disabled, skipping initialization");
                return;
            }

            var compat = CheckCompatibility(plugin.Manifest);
            if (!compat.IsCompatible)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Builtin plugin {pluginId} is incompatible: {compat.Reason}");
                _incompatiblePlugins.Add(pluginId);
                return;
            }

            try
            {
                if (_host is not null)
                    plugin.Initialize(_host);

                _plugins[pluginId] = plugin;
                App.Logger.WriteLine(LOG_IDENT, $"Registered builtin plugin: {pluginId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                App.Logger.WriteLine(LOG_IDENT, $"Failed to initialize builtin plugin: {pluginId}");
            }
        }

        public void EnablePlugin(string pluginId)
        {
            const string LOG_IDENT = "PluginManager::EnablePlugin";

            if (_incompatiblePlugins.Contains(pluginId))
                return;

            _enabledPlugins.Add(pluginId);

            var enabledList = App.Settings.Prop.EnabledPlugins;
            if (!enabledList.Contains(pluginId))
                enabledList.Add(pluginId);

            App.Settings.Save();

            if (_plugins.ContainsKey(pluginId))
                return;

            if (!_manifests.TryGetValue(pluginId, out var manifest))
                return;

            var compat = CheckCompatibility(manifest);
            if (!compat.IsCompatible)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Plugin {pluginId} is incompatible: {compat.Reason}");
                _incompatiblePlugins.Add(pluginId);
                return;
            }

            if (_pluginFactories.TryGetValue(pluginId, out var factory))
            {
                try
                {
                    var plugin = factory();
                    if (_host is not null)
                        plugin.Initialize(_host);
                    _plugins[pluginId] = plugin;
                    App.Logger.WriteLine(LOG_IDENT, $"Re-initialized builtin plugin: {pluginId}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to re-initialize plugin: {pluginId}");
                }

                return;
            }

            string pluginDir = Path.Combine(_pluginsDirectory, pluginId);
            string dllPath = Path.Combine(pluginDir, $"{pluginId}.dll");

            if (!File.Exists(dllPath))
                return;

            try
            {
                var assembly = System.Reflection.Assembly.LoadFrom(dllPath);
                var pluginType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                if (pluginType is null)
                    return;

                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                if (_host is not null)
                    plugin.Initialize(_host);
                _plugins[pluginId] = plugin;
                App.Logger.WriteLine(LOG_IDENT, $"Re-initialized plugin: {pluginId}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                App.Logger.WriteLine(LOG_IDENT, $"Failed to re-initialize plugin: {pluginId}");
            }
        }

        public void DisablePlugin(string pluginId)
        {
            _enabledPlugins.Remove(pluginId);

            var enabledList = App.Settings.Prop.EnabledPlugins;
            enabledList.Remove(pluginId);

            App.Settings.Save();

            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    plugin.OnShutdown();
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("PluginManager::DisablePlugin", ex);
                }

                _plugins.Remove(pluginId);
            }
        }

        public void ShutdownAll()
        {
            const string LOG_IDENT = "PluginManager::ShutdownAll";

            foreach (var plugin in _plugins.Values)
            {
                try
                {
                    plugin.OnShutdown();
                    plugin.Dispose();
                    App.Logger.WriteLine(LOG_IDENT, $"Shut down plugin: {plugin.Manifest.Id}");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            _plugins.Clear();
        }

        public PluginManifest? GetManifest(string pluginId)
        {
            _manifests.TryGetValue(pluginId, out var manifest);
            return manifest;
        }

        public static CompatibilityResult CheckCompatibility(PluginManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.ApiVersion))
                return CompatibilityResult.Incompatible("Missing API version");

            if (!Version.TryParse(manifest.ApiVersion, out var pluginApiVersion))
                return CompatibilityResult.Incompatible($"Malformed API version: {manifest.ApiVersion}");

            if (!Version.TryParse(SupportedApiVersion, out var hostApiVersion))
                return CompatibilityResult.Incompatible("Host API version is invalid");

            if (pluginApiVersion.Major != hostApiVersion.Major)
                return CompatibilityResult.Incompatible(
                    $"Requires API v{manifest.ApiVersion}, host supports v{SupportedApiVersion}");

            if (!string.IsNullOrEmpty(manifest.MinRainstrapVersion))
            {
                if (!Version.TryParse(manifest.MinRainstrapVersion, out var minVersion))
                    return CompatibilityResult.Incompatible($"Malformed minimum version: {manifest.MinRainstrapVersion}");

                if (!Version.TryParse(App.Version, out var hostVersion))
                    return CompatibilityResult.Incompatible("Cannot determine host version");

                if (hostVersion < minVersion)
                    return CompatibilityResult.Incompatible(
                        $"Requires Rainstrap v{manifest.MinRainstrapVersion}, running v{App.Version}");
            }

            return CompatibilityResult.Compatible();
        }
    }

    public class CompatibilityResult
    {
        public bool IsCompatible { get; }
        public string Reason { get; }

        private CompatibilityResult(bool isCompatible, string reason)
        {
            IsCompatible = isCompatible;
            Reason = reason;
        }

        public static CompatibilityResult Compatible() => new(true, "");
        public static CompatibilityResult Incompatible(string reason) => new(false, reason);
    }
}
