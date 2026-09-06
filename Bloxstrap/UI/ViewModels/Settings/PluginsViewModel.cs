using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Plugins;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class PluginCardViewModel : NotifyPropertyChangedViewModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "";
        public string Version { get; set; } = "";
        public string PermissionsText { get; set; } = "";
        public bool IsOfficial { get; set; }
        public bool IsVerified { get; set; }
        public bool IsInstalled { get; set; }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(EnabledStatusVisibility));
                OnPropertyChanged(nameof(DisabledStatusVisibility));
            }
        }

        private bool _hasUpdate = false;
        public bool HasUpdate
        {
            get => _hasUpdate;
            set
            {
                _hasUpdate = value;
                OnPropertyChanged(nameof(HasUpdate));
                OnPropertyChanged(nameof(UpdateVisibility));
            }
        }

        public string? UpdateVersion { get; set; }

        private bool _isInstalling = false;
        public bool IsInstalling
        {
            get => _isInstalling;
            set
            {
                _isInstalling = value;
                OnPropertyChanged(nameof(IsInstalling));
                OnPropertyChanged(nameof(InstallButtonEnabled));
            }
        }

        private bool _isCatalogPlugin = false;
        public bool IsCatalogPlugin
        {
            get => _isCatalogPlugin;
            set
            {
                _isCatalogPlugin = value;
                OnPropertyChanged(nameof(IsCatalogPlugin));
                OnPropertyChanged(nameof(InstalledActionsVisibility));
                OnPropertyChanged(nameof(AvailableActionsVisibility));
            }
        }

        public Visibility OfficialBadgeVisibility => IsOfficial ? Visibility.Visible : Visibility.Collapsed;
        public Visibility VerifiedBadgeVisibility => IsVerified ? Visibility.Visible : Visibility.Collapsed;
        public Visibility EnabledStatusVisibility => IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DisabledStatusVisibility => IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        public Visibility UpdateVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InstalledActionsVisibility => IsCatalogPlugin ? Visibility.Collapsed : Visibility.Visible;
        public Visibility AvailableActionsVisibility => IsCatalogPlugin ? Visibility.Visible : Visibility.Collapsed;
        public bool InstallButtonEnabled => !IsInstalling;
    }

    public class PluginsViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<PluginCardViewModel> InstalledPlugins { get; } = new();
        public ObservableCollection<PluginCardViewModel> AvailablePlugins { get; } = new();

        private Visibility _noPluginsVisibility = Visibility.Collapsed;
        public Visibility NoPluginsVisibility
        {
            get => _noPluginsVisibility;
            set { _noPluginsVisibility = value; OnPropertyChanged(nameof(NoPluginsVisibility)); }
        }

        private Visibility _noAvailableVisibility = Visibility.Collapsed;
        public Visibility NoAvailableVisibility
        {
            get => _noAvailableVisibility;
            set { _noAvailableVisibility = value; OnPropertyChanged(nameof(NoAvailableVisibility)); }
        }

        private Visibility _catalogueErrorVisibility = Visibility.Collapsed;
        public Visibility CatalogueErrorVisibility
        {
            get => _catalogueErrorVisibility;
            set { _catalogueErrorVisibility = value; OnPropertyChanged(nameof(CatalogueErrorVisibility)); }
        }

        private string _catalogueErrorText = "";
        public string CatalogueErrorText
        {
            get => _catalogueErrorText;
            set { _catalogueErrorText = value; OnPropertyChanged(nameof(CatalogueErrorText)); }
        }

        private bool _isLoadingCatalogue = false;
        public bool IsLoadingCatalogue
        {
            get => _isLoadingCatalogue;
            set
            {
                _isLoadingCatalogue = value;
                OnPropertyChanged(nameof(IsLoadingCatalogue));
                OnPropertyChanged(nameof(LoadingVisibility));
            }
        }

        public Visibility LoadingVisibility => IsLoadingCatalogue ? Visibility.Visible : Visibility.Collapsed;

        public ICommand RefreshCommand => new RelayCommand(() => _ = RefreshAsync());

        public PluginsViewModel()
        {
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            InstalledPlugins.Clear();
            AvailablePlugins.Clear();

            if (App.PluginManager is null)
            {
                NoPluginsVisibility = Visibility.Visible;
                NoAvailableVisibility = Visibility.Visible;
                return;
            }

            foreach (var manifest in App.PluginManager.Manifests.Values)
            {
                var card = new PluginCardViewModel
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Description = manifest.Description,
                    Author = manifest.Author,
                    Version = manifest.Version,
                    IsOfficial = manifest.IsOfficial,
                    IsVerified = manifest.Verified,
                    PermissionsText = manifest.Permissions.Count > 0
                        ? string.Join(", ", manifest.Permissions.Select(FormatPermission))
                        : Strings.Plugins_None,
                    IsEnabled = App.PluginManager.IsPluginEnabled(manifest.Id),
                    IsCatalogPlugin = false
                };

                card.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PluginCardViewModel.IsEnabled))
                        TogglePlugin(card.Id, card.IsEnabled);
                };

                InstalledPlugins.Add(card);
            }

            NoPluginsVisibility = InstalledPlugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            await LoadCatalogueAsync();
        }

        private async Task LoadCatalogueAsync()
        {
            if (App.PluginManager is null)
                return;

            IsLoadingCatalogue = true;
            CatalogueErrorVisibility = Visibility.Collapsed;

            try
            {
                var catalog = await App.PluginManager.CatalogClient.GetCatalogAsync();

                if (catalog is null)
                {
                    CatalogueErrorText = Strings.Plugins_CatalogOffline;
                    CatalogueErrorVisibility = Visibility.Visible;
                    NoAvailableVisibility = Visibility.Visible;
                    return;
                }

                var installedIds = new HashSet<string>(App.PluginManager.Manifests.Keys);

                foreach (var entry in catalog.Plugins)
                {
                    if (installedIds.Contains(entry.Id))
                    {
                        var existing = InstalledPlugins.FirstOrDefault(p => p.Id == entry.Id);
                        if (existing is not null && !string.IsNullOrEmpty(entry.Version))
                        {
                            if (Version.TryParse(existing.Version, out var localVer)
                                && Version.TryParse(entry.Version, out var remoteVer)
                                && remoteVer > localVer)
                            {
                                existing.HasUpdate = true;
                                existing.UpdateVersion = entry.Version;
                            }
                        }
                        continue;
                    }

                    var card = new PluginCardViewModel
                    {
                        Id = entry.Id,
                        Name = entry.Name,
                        Description = entry.Description,
                        Author = entry.Author,
                        Version = entry.Version,
                        IsOfficial = entry.Official,
                        IsVerified = entry.Verified,
                        PermissionsText = entry.Permissions.Count > 0
                            ? string.Join(", ", entry.Permissions.Select(FormatPermission))
                            : Strings.Plugins_None,
                        IsEnabled = false,
                        IsInstalled = false,
                        IsCatalogPlugin = true
                    };

                    AvailablePlugins.Add(card);
                }

                NoAvailableVisibility = AvailablePlugins.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("PluginsViewModel::LoadCatalogueAsync", ex);
                CatalogueErrorText = Strings.Plugins_CatalogOffline;
                CatalogueErrorVisibility = Visibility.Visible;
                NoAvailableVisibility = Visibility.Visible;
            }
            finally
            {
                IsLoadingCatalogue = false;
            }
        }

        public void TogglePlugin(string pluginId, bool enable)
        {
            if (App.PluginManager is null)
                return;

            bool currentlyEnabled = App.PluginManager.IsPluginEnabled(pluginId);

            if (enable && !currentlyEnabled)
            {
                App.PluginManager.RequestEnable(pluginId);
                PromptRestart();
            }
            else if (!enable && currentlyEnabled)
            {
                App.PluginManager.RequestDisable(pluginId);
                PromptRestart();
            }
        }

        public async Task UpdatePluginAsync(string pluginId)
        {
            if (App.PluginManager is null)
                return;

            var card = InstalledPlugins.FirstOrDefault(p => p.Id == pluginId);
            if (card is null || !card.HasUpdate)
                return;

            var catalog = await App.PluginManager.CatalogClient.GetCatalogAsync();
            var entry = catalog?.Plugins.FirstOrDefault(p => p.Id == pluginId);

            if (entry is null)
            {
                Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
                return;
            }

            card.IsInstalling = true;

            try
            {
                string tempPackage = Path.Combine(Path.GetTempPath(), "Rainstrap", "PluginDownload", $"{entry.Id}.rspkg");

                bool downloaded = await App.PluginManager.CatalogClient.DownloadPackageAsync(
                    entry.PackageUrl, tempPackage, entry.Sha256);

                if (!downloaded)
                {
                    Frontend.ShowMessageBox(Strings.Plugins_DownloadFailed, MessageBoxImage.Error);
                    return;
                }

                if (!PluginInstaller.VerifyPackageHash(tempPackage, entry.Sha256))
                {
                    Frontend.ShowMessageBox(Strings.Plugins_HashMismatch, MessageBoxImage.Error);
                    File.Delete(tempPackage);
                    return;
                }

                bool updated = App.PluginManager.UpdatePlugin(pluginId, tempPackage);

                File.Delete(tempPackage);

                if (!updated)
                {
                    Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
                    return;
                }

                PromptRestart();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("PluginsViewModel::UpdatePluginAsync", ex);
                Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
            }
            finally
            {
                card.IsInstalling = false;
            }
        }

        private static void PromptRestart()
        {
            var result = Frontend.ShowMessageBox(
                Strings.Plugins_RestartRequired,
                MessageBoxImage.Information,
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
                RestartRainstrap();
        }

        public void UninstallPlugin(string pluginId)
        {
            if (App.PluginManager is null)
                return;

            App.PluginManager.RequestUninstall(pluginId);
            PromptRestart();
        }

        public async Task InstallPluginAsync(PluginCardViewModel card)
        {
            if (App.PluginManager is null)
                return;

            var catalog = await App.PluginManager.CatalogClient.GetCatalogAsync();
            var entry = catalog?.Plugins.FirstOrDefault(p => p.Id == card.Id);

            if (entry is null)
            {
                Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
                return;
            }

            var manifest = PluginManager.CheckCompatibility(new PluginManifest
            {
                Id = entry.Id,
                Version = entry.Version,
                ApiVersion = entry.ApiVersion,
                MinRainstrapVersion = entry.MinRainstrapVersion ?? ""
            });

            if (!manifest.IsCompatible)
            {
                Frontend.ShowMessageBox(
                    string.Format(Strings.Plugins_Incompatible, entry.Name, manifest.Reason),
                    MessageBoxImage.Warning);
                return;
            }

            card.IsInstalling = true;

            try
            {
                string tempPackage = Path.Combine(Path.GetTempPath(), "Rainstrap", "PluginDownload", $"{entry.Id}.rspkg");

                bool downloaded = await App.PluginManager.CatalogClient.DownloadPackageAsync(
                    entry.PackageUrl, tempPackage, entry.Sha256);

                if (!downloaded)
                {
                    Frontend.ShowMessageBox(Strings.Plugins_DownloadFailed, MessageBoxImage.Error);
                    return;
                }

                if (!PluginInstaller.VerifyPackageHash(tempPackage, entry.Sha256))
                {
                    Frontend.ShowMessageBox(Strings.Plugins_HashMismatch, MessageBoxImage.Error);
                    File.Delete(tempPackage);
                    return;
                }

                var installed = App.PluginManager.InstallPlugin(tempPackage);

                File.Delete(tempPackage);

                if (installed is null)
                {
                    Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
                    return;
                }

                App.PluginManager.RequestEnable(installed.Id);

                var result = Frontend.ShowMessageBox(
                    Strings.Plugins_RestartRequired,
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                    RestartRainstrap();

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("PluginsViewModel::InstallPluginAsync", ex);
                Frontend.ShowMessageBox(Strings.Plugins_InstallFailed, MessageBoxImage.Error);
            }
            finally
            {
                card.IsInstalling = false;
            }
        }

        private static void RestartRainstrap()
        {
            string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

            if (!string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Process.Start(exePath);
            }

            App.SoftTerminate();
        }

        private static string FormatPermission(string perm) => perm switch
        {
            PluginCapabilities.ProcessManagement => "Process Management",
            PluginCapabilities.ScreenCapture => "Screen Capture",
            PluginCapabilities.Filesystem => "Filesystem Access",
            PluginCapabilities.CredentialStorage => "Credential Storage",
            PluginCapabilities.CookieAccess => "Cookie Access",
            _ => perm
        };
    }
}
