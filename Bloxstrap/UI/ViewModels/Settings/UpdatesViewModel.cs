using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Enums;
using Bloxstrap.Models.APIs.GitHub;
using Bloxstrap.Models.APIs.Roblox;
using Bloxstrap.Models.Persistable;
using Bloxstrap.RobloxInterfaces;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// One row in the version-history list: a previously installed player
    /// build, annotated with its current/latest/previous status and whether a
    /// rollback to it is actually possible (i.e. Roblox still serves its
    /// package manifest).
    /// </summary>
    public class VersionHistoryCard : NotifyPropertyChangedViewModel
    {
        private readonly Func<VersionHistoryCard, Task> _rollbackCallback;

        public string VersionGuid { get; init; } = "";

        public string DisplayVersion { get; set; } = "";
        public string InstalledText { get; set; } = "";

        private bool _isCurrent;
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                _isCurrent = value;
                OnPropertyChanged(nameof(IsCurrent));
                OnPropertyChanged(nameof(StateBadgeText));
            }
        }

        public bool IsLatest { get; set; }

        public string StateBadgeText =>
            IsCurrent ? Strings.Updates_History_BadgeCurrent :
            IsLatest ? Strings.Updates_History_BadgeLatest :
                       Strings.Updates_History_BadgePrevious;

        // Rollback availability probe state: null = checking, true/false = result.
        private bool? _rollbackAvailable;
        public bool? RollbackAvailable
        {
            get => _rollbackAvailable;
            set
            {
                _rollbackAvailable = value;
                OnPropertyChanged(nameof(RollbackAvailable));
                OnPropertyChanged(nameof(RollbackStatusText));
                OnPropertyChanged(nameof(RollbackButtonVisibility));
                OnPropertyChanged(nameof(RollbackProbeVisibility));
            }
        }

        public string RollbackStatusText =>
            RollbackAvailable is null ? Strings.Updates_History_CheckingRollback :
            RollbackAvailable == true ? Strings.Updates_History_RollbackAvailable :
                                        Strings.Updates_History_RollbackUnavailable;

        public Visibility RollbackButtonVisibility =>
            !IsCurrent && RollbackAvailable == true ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RollbackProbeVisibility =>
            !IsCurrent ? Visibility.Visible : Visibility.Collapsed;

        public ICommand RollbackCommand => new AsyncRelayCommand(() => _rollbackCallback(this));

        public VersionHistoryCard(Func<VersionHistoryCard, Task> rollbackCallback)
        {
            _rollbackCallback = rollbackCallback;
        }
    }

    public class UpdatesViewModel : NotifyPropertyChangedViewModel
    {
        private ClientVersion? _availableVersion;
        private GithubRelease? _latestRainstrapRelease;

        public ObservableCollection<VersionHistoryCard> RecentVersions { get; } = new();

        public IReadOnlyDictionary<UpgradeMode, string> UpgradeModes => new Dictionary<UpgradeMode, string>
        {
            { UpgradeMode.Automatic, Strings.Updates_UpgradeMode_Automatic },
            { UpgradeMode.Notify, Strings.Updates_UpgradeMode_Notify }
        };

        public UpgradeMode SelectedUpgradeMode
        {
            get => App.Settings.Prop.UpgradeMode;
            set
            {
                App.Settings.Prop.UpgradeMode = value;
                App.Settings.Save();
            }
        }

        public string CurrentVersionText
        {
            get
            {
                string version = Utilities.GetRobloxVersionStr(false);

                if (String.IsNullOrEmpty(version))
                    return Strings.Common_RobloxNotInstalled;

                return $"{version}  ({App.RobloxState.Prop.Player.VersionGuid})";
            }
        }

        public string ChannelText => String.Format(Strings.Updates_CurrentChannel, Deployment.Channel);

        public string LastCheckedText
        {
            get
            {
                DateTime? lastChecked = App.State.Prop.LastUpdateCheckUtc;

                return lastChecked is null
                    ? String.Format(Strings.Updates_LastChecked, Strings.Updates_Never)
                    : String.Format(Strings.Updates_LastChecked, lastChecked.Value.ToLocalTime().ToString("g"));
            }
        }

        public string AvailableVersionText
        {
            get
            {
                if (_availableVersion is null)
                    return Strings.Updates_NotChecked;

                return $"{_availableVersion.Version}  ({_availableVersion.VersionGuid})";
            }
        }

        // UpdateStatus
        private string _statusTitle = "";
        public string StatusTitle
        {
            get => _statusTitle;
            set
            {
                _statusTitle = value;
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }

        private string _statusDescription = "";
        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                _statusDescription = value;
                OnPropertyChanged(nameof(StatusDescription));
            }
        }

        private Visibility _statusVisibility = Visibility.Collapsed;
        public Visibility StatusVisibility
        {
            get => _statusVisibility;
            set
            {
                _statusVisibility = value;
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }

        private bool _isUpToDate = false;
        public bool IsUpToDate
        {
            get => _isUpToDate;
            set
            {
                _isUpToDate = value;
                OnPropertyChanged(nameof(IsUpToDate));
            }
        }

        private Visibility _errorVisibility = Visibility.Collapsed;
        public Visibility ErrorVisibility
        {
            get => _errorVisibility;
            set
            {
                _errorVisibility = value;
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
                ErrorVisibility = String.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private bool _isChecking = false;
        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                _isChecking = value;
                OnPropertyChanged(nameof(IsChecking));
                OnPropertyChanged(nameof(CheckNowEnabled));
                OnPropertyChanged(nameof(CheckButtonText));
            }
        }

        public string CheckButtonText => IsChecking ? Strings.Updates_Checking : Strings.Updates_CheckNow;

        public bool CheckNowEnabled => !_isChecking;

        public ICommand CheckForUpdatesCommand => new AsyncRelayCommand(async () => await CheckForUpdatesAsync());

        // ─── Rainstrap self-update ──────────────────────────────────────

        private bool _isCheckingRainstrap = false;
        public bool IsCheckingRainstrap
        {
            get => _isCheckingRainstrap;
            set
            {
                _isCheckingRainstrap = value;
                OnPropertyChanged(nameof(IsCheckingRainstrap));
                OnPropertyChanged(nameof(RainstrapCheckButtonText));
                OnPropertyChanged(nameof(RainstrapCheckEnabled));
            }
        }

        public string RainstrapCheckButtonText => IsCheckingRainstrap
            ? Strings.Updates_Rainstrap_Checking
            : Strings.Updates_Rainstrap_CheckNow;

        public bool RainstrapCheckEnabled => !_isCheckingRainstrap && !_isInstallingRainstrap;

        public string RainstrapCurrentVersionText => $"v{App.Version}";

        private string _rainstrapStatusText = "";
        public string RainstrapStatusText
        {
            get => _rainstrapStatusText;
            set
            {
                _rainstrapStatusText = value;
                OnPropertyChanged(nameof(RainstrapStatusText));
            }
        }

        private string _rainstrapLatestVersionText = "";
        public string RainstrapLatestVersionText
        {
            get => _rainstrapLatestVersionText;
            set
            {
                _rainstrapLatestVersionText = value;
                OnPropertyChanged(nameof(RainstrapLatestVersionText));
                OnPropertyChanged(nameof(RainstrapLatestVersionVisibility));
            }
        }

        public Visibility RainstrapLatestVersionVisibility =>
            String.IsNullOrEmpty(_rainstrapLatestVersionText) ? Visibility.Collapsed : Visibility.Visible;

        private string _rainstrapChangelog = "";
        public string RainstrapChangelog
        {
            get => _rainstrapChangelog;
            set
            {
                _rainstrapChangelog = value;
                OnPropertyChanged(nameof(RainstrapChangelog));
                OnPropertyChanged(nameof(RainstrapChangelogVisibility));
            }
        }

        public Visibility RainstrapChangelogVisibility =>
            String.IsNullOrEmpty(_rainstrapChangelog) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isRainstrapUpToDate = false;
        public bool IsRainstrapUpToDate
        {
            get => _isRainstrapUpToDate;
            set
            {
                _isRainstrapUpToDate = value;
                OnPropertyChanged(nameof(IsRainstrapUpToDate));
                OnPropertyChanged(nameof(RainstrapUpdateButtonVisibility));
            }
        }

        public Visibility RainstrapUpdateButtonVisibility =>
            IsRainstrapUpToDate || IsCheckingRainstrap || _isInstallingRainstrap
                ? Visibility.Collapsed
                : Visibility.Visible;

        private bool _isInstallingRainstrap = false;
        public bool IsInstallingRainstrap
        {
            get => _isInstallingRainstrap;
            set
            {
                _isInstallingRainstrap = value;
                OnPropertyChanged(nameof(IsInstallingRainstrap));
                OnPropertyChanged(nameof(RainstrapCheckEnabled));
                OnPropertyChanged(nameof(RainstrapUpdateButtonVisibility));
                OnPropertyChanged(nameof(RainstrapUpdateButtonText));
            }
        }

        public string RainstrapUpdateButtonText => IsInstallingRainstrap
            ? Strings.Updates_Rainstrap_Installing
            : Strings.Updates_Rainstrap_UpdateNow;

        private string _rainstrapError = "";
        public string RainstrapError
        {
            get => _rainstrapError;
            set
            {
                _rainstrapError = value;
                OnPropertyChanged(nameof(RainstrapError));
                OnPropertyChanged(nameof(RainstrapErrorVisibility));
            }
        }

        public Visibility RainstrapErrorVisibility =>
            String.IsNullOrEmpty(_rainstrapError) ? Visibility.Collapsed : Visibility.Visible;

        private string _rainstrapLastCheckedText = "";
        public string RainstrapLastCheckedText
        {
            get => _rainstrapLastCheckedText;
            set
            {
                _rainstrapLastCheckedText = value;
                OnPropertyChanged(nameof(RainstrapLastCheckedText));
            }
        }

        public ICommand CheckRainstrapUpdatesCommand => new AsyncRelayCommand(async () => await CheckForRainstrapUpdatesAsync());
        public ICommand InstallRainstrapUpdateCommand => new AsyncRelayCommand(async () => await InstallRainstrapUpdateAsync());
        public ICommand ViewRainstrapChangelogCommand => new RelayCommand(() =>
        {
            if (_latestRainstrapRelease is not null)
                Utilities.ShellExecute($"https://github.com/{App.ProjectRepository}/releases/tag/{_latestRainstrapRelease.TagName}");
        });

        public UpdatesViewModel()
        {
            RefreshCurrentVersion();
            BuildVersionHistory();
            LoadCachedRainstrapCheck();
        }

        /// <summary>
        /// Loads cached Rainstrap update check from State.json.
        /// If the cache is less than 5 minutes old, shows cached result immediately.
        /// Otherwise, auto-checks in the background.
        /// </summary>
        private void LoadCachedRainstrapCheck()
        {
            var lastCheck = App.State.Prop.LastRainstrapCheckUtc;
            var cachedTag = App.State.Prop.LastRainstrapReleaseTag;
            var cachedBody = App.State.Prop.LastRainstrapReleaseBody;

            if (lastCheck is not null && !String.IsNullOrEmpty(cachedTag))
            {
                var age = DateTime.UtcNow - lastCheck.Value;
                RainstrapLastCheckedText = String.Format(Strings.Updates_LastChecked, lastCheck.Value.ToLocalTime().ToString("g"));

                // If cache is fresh (<5 min), show cached result immediately
                if (age.TotalMinutes < 5)
                {
                    ApplyRainstrapCachedResult(cachedTag, cachedBody);
                    return;
                }
            }

            // Cache is stale or missing — auto-check in background
#pragma warning disable CS4014
            CheckForRainstrapUpdatesAsync();
#pragma warning restore CS4014
        }

        /// <summary>
        /// Applies a cached release result without hitting the network.
        /// </summary>
        private void ApplyRainstrapCachedResult(string tag, string? body)
        {
            var latestVersion = Utilities.GetVersionFromString(tag);
            var currentVersion = Utilities.GetVersionFromString(App.Version);

            int comparison = currentVersion.CompareTo(latestVersion);

            if (comparison >= 0)
            {
                IsRainstrapUpToDate = true;
                RainstrapStatusText = Strings.Updates_Rainstrap_UpToDate;
            }
            else
            {
                RainstrapStatusText = String.Format(Strings.Updates_Rainstrap_UpdateAvailable, tag);
                RainstrapLatestVersionText = tag;
                RainstrapChangelog = body ?? "";
            }

            // Build a minimal release object for install support
            _latestRainstrapRelease = new GithubRelease
            {
                TagName = tag,
                Body = body ?? "",
                Name = tag,
                Assets = new List<GithubReleaseAsset>
                {
                    new GithubReleaseAsset
                    {
                        Name = "Rainstrap.exe",
                        BrowserDownloadUrl = $"https://github.com/{App.ProjectRepository}/releases/download/{tag}/Rainstrap.exe"
                    }
                }
            };
        }

        /// <summary>
        /// Re-reads the installed version (it can change after Rainstrap updates Roblox).
        /// </summary>
        public void RefreshCurrentVersion()
        {
            OnPropertyChanged(nameof(CurrentVersionText));
            OnPropertyChanged(nameof(ChannelText));

            // re-evaluate the cached verdict against the current install
            if (_availableVersion is not null)
                ApplyVerdict(_availableVersion);

            // rebuild if the install changed underneath us (e.g. a rollback
            // finished in the background and the page was re-opened)
            string installedGuid = App.RobloxState.Prop.Player.VersionGuid ?? "";
            if (installedGuid != _installedGuid)
                BuildVersionHistory();
        }

        // ─── Version history / rollback ─────────────────────────────────────

        private string _installedGuid = "";

        /// <summary>
        /// Rebuilds the version-history list from locally recorded installs
        /// (most recent first), tagging each entry as current/latest/previous.
        /// Rollback availability is probed asynchronously per entry.
        /// </summary>
        public void BuildVersionHistory()
        {
            RecentVersions.Clear();

            _installedGuid = App.RobloxState.Prop.Player.VersionGuid ?? "";
            var latestGuid = _availableVersion?.VersionGuid ?? "";

            var history = App.RobloxState.Prop.PlayerVersionHistory;

            foreach (var entry in history.AsEnumerable().Reverse())
            {
                if (String.IsNullOrEmpty(entry.VersionGuid))
                    continue;

                var card = new VersionHistoryCard(RollbackToAsync)
                {
                    VersionGuid = entry.VersionGuid,
                    DisplayVersion = String.IsNullOrEmpty(entry.Version) ? "" : $"Roblox {entry.Version}",
                    InstalledText = String.Format(
                        Strings.Updates_History_InstalledOn,
                        entry.InstalledAtUtc.ToLocalTime().ToString("g")),
                    IsCurrent = entry.VersionGuid == _installedGuid,
                    IsLatest = latestGuid != "" && entry.VersionGuid == latestGuid && entry.VersionGuid != _installedGuid,
                };

                if (!card.IsCurrent)
                {
                    card.RollbackAvailable = null;
#pragma warning disable CS4014 // fire-and-forget probe; results are marshalled back via the UI thread
                    ProbeRollbackAvailabilityAsync(card);
#pragma warning restore CS4014
                }
                else
                {
                    card.RollbackAvailable = false;
                }

                RecentVersions.Add(card);
            }

            OnPropertyChanged(nameof(HistoryEmptyVisibility));
        }

        public Visibility HistoryEmptyVisibility =>
            RecentVersions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HistoryListVisibility =>
            RecentVersions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// A rollback target is only genuinely supported when Roblox's CDN
        /// still serves its package manifest - otherwise installing it would
        /// fail halfway. The probe validates before ever offering the button.
        /// </summary>
        private async Task ProbeRollbackAvailabilityAsync(VersionHistoryCard card)
        {
            const string LOG_IDENT = "UpdatesViewModel::ProbeRollbackAvailability";

            try
            {
                if (String.IsNullOrEmpty(Deployment.BaseUrl))
                    await Deployment.InitializeConnectivity();

                if (String.IsNullOrEmpty(Deployment.BaseUrl))
                {
                    card.RollbackAvailable = false;
                    return;
                }

                Uri manifestUrl = new(Deployment.GetLocation($"/{card.VersionGuid}-rbxPkgManifest.txt"));
                HttpResponseMessage response = await App.HttpClient.GetAsync(manifestUrl);

                bool available = response.IsSuccessStatusCode;
                card.RollbackAvailable = available;

                App.Logger.WriteLine(LOG_IDENT, $"Rollback to {card.VersionGuid}: {(available ? "available" : "unavailable")} (HTTP {(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Rollback probe for {card.VersionGuid} failed: {ex.Message}");
                card.RollbackAvailable = false;
            }
        }

        /// <summary>
        /// Explicitly reinstalls a previous version using the existing
        /// bootstrapper flow (-version &lt;guid&gt;), silently and without launching
        /// Roblox. Never automatic; always confirmed by the user first.
        /// </summary>
        private async Task RollbackToAsync(VersionHistoryCard card)
        {
            const string LOG_IDENT = "UpdatesViewModel::RollbackTo";

            if (card.IsCurrent || card.RollbackAvailable != true)
                return;

            // Re-validate right before acting: availability may have changed.
            card.RollbackAvailable = null;
            await ProbeRollbackAvailabilityAsync(card);

            if (card.RollbackAvailable != true)
            {
                Frontend.ShowMessageBox(
                    Strings.Updates_History_RollbackUnavailable,
                    MessageBoxImage.Warning
                );
                return;
            }

            string display = String.IsNullOrEmpty(card.DisplayVersion)
                ? card.VersionGuid
                : card.DisplayVersion.Replace("Roblox ", "");

            MessageBoxResult choice = Frontend.ShowMessageBox(
                String.Format(Strings.Updates_History_RollbackConfirmText, display),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo,
                MessageBoxResult.No
            );

            if (choice != MessageBoxResult.Yes)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"-version {card.VersionGuid} -quiet -nolaunch",
                    UseShellExecute = false
                });

                App.Logger.WriteLine(LOG_IDENT, $"Rollback to {card.VersionGuid} started");

                Frontend.ShowMessageBox(
                    String.Format(Strings.Updates_History_RollbackStarted, display),
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    String.Format(Strings.Updates_History_RollbackFailed, ex.Message),
                    MessageBoxImage.Error
                );
            }
        }

        public async Task CheckForUpdatesAsync()
        {
            if (_isChecking)
                return;

            ErrorMessage = "";
            IsChecking = true;

            // NOTE: any previously fetched verdict/available version is intentionally
            // kept visible while re-checking, and on failure.

            long startTimestamp = Stopwatch.GetTimestamp();

            try
            {
                // forceRefresh: the deploy-info cache is process-lifetime, and an
                // explicit check must reflect what Roblox is serving right now.
                ClientVersion clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel, forceRefresh: true);

                _availableVersion = clientVersion;

                double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
                ApiLatencyText = String.Format(Strings.Updates_ApiLatency, (int)Math.Round(elapsedMs));

                App.State.Prop.LastUpdateCheckUtc = DateTime.UtcNow;
                App.State.Save();

                ApplyVerdict(clientVersion);
                OnPropertyChanged(nameof(LastCheckedText));

                // refresh latest-version badges in the history list
                BuildVersionHistory();
            }
            catch (Exception ex)
            {
                // network failures (timeouts, DNS, blocked CDN) must never escape into
                // the WPF navigation stack - report the failure and keep the last
                // known good data on screen.
                App.Logger.WriteLine("UpdatesViewModel::CheckForUpdates", $"Update check failed: {ex.Message}");

                ErrorMessage = String.Format(Strings.Updates_CheckFailed, ex.Message);

                StatusTitle = Strings.Updates_CheckFailedTitle;
                StatusDescription = String.Format(Strings.Updates_CheckFailed, ex.Message);
                StatusVisibility = Visibility.Visible;
                IsUpToDate = false;

                // LastUpdateCheckUtc intentionally not updated: it reflects the last
                // SUCCESSFUL check only, and the previously fetched available version
                // stays displayed in the Available version row.
            }
            finally
            {
                IsChecking = false;
            }
        }

        private void ApplyVerdict(ClientVersion available)
        {
            string installedGuid = App.RobloxState.Prop.Player.VersionGuid ?? "";

            if (String.IsNullOrEmpty(installedGuid))
            {
                StatusTitle = Strings.Common_RobloxNotInstalled;
                StatusDescription = "";
                StatusVisibility = Visibility.Visible;
                IsUpToDate = false;
                return;
            }

            if (installedGuid == available.VersionGuid)
            {
                StatusTitle = Strings.Updates_UpToDate;
                StatusDescription = Strings.Updates_UpToDateDescription;
                IsUpToDate = true;
            }
            else
            {
                StatusTitle = Strings.Updates_UpdateAvailable;
                StatusDescription = Strings.Updates_UpdateAvailableDescription;
                IsUpToDate = false;
            }

            StatusVisibility = Visibility.Visible;
        }

        private string _apiLatencyText = Strings.Updates_LatencyNotMeasured;
        public string ApiLatencyText
        {
            get => _apiLatencyText;
            set
            {
                _apiLatencyText = value;
                OnPropertyChanged(nameof(ApiLatencyText));
            }
        }

        // ─── Rainstrap self-update methods ──────────────────────────────

        public async Task CheckForRainstrapUpdatesAsync()
        {
            const string LOG_IDENT = "UpdatesViewModel::CheckForRainstrapUpdates";

            if (IsCheckingRainstrap || _isInstallingRainstrap)
                return;

            RainstrapError = "";
            RainstrapStatusText = "";
            RainstrapLatestVersionText = "";
            RainstrapChangelog = "";
            IsRainstrapUpToDate = false;
            IsCheckingRainstrap = true;

            try
            {
                // Use a short timeout so the UI doesn't hang on slow connections
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var checkClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                checkClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Rainstrap", App.Version));

                var url = $"https://api.github.com/repos/{App.ProjectRepository}/releases/latest";
                var response = await checkClient.GetAsync(url, cts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GithubRelease>(json);

                if (release is null || release.Assets is null)
                {
                    RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, "Invalid response from GitHub");
                    return;
                }

                _latestRainstrapRelease = release;

                var latestVersion = Utilities.GetVersionFromString(release.TagName);
                var currentVersion = Utilities.GetVersionFromString(App.Version);

                int comparison = currentVersion.CompareTo(latestVersion);

                if (comparison >= 0)
                {
                    IsRainstrapUpToDate = true;
                    RainstrapStatusText = Strings.Updates_Rainstrap_UpToDate;
                }
                else
                {
                    RainstrapStatusText = String.Format(Strings.Updates_Rainstrap_UpdateAvailable, release.TagName);
                    RainstrapLatestVersionText = release.TagName;
                    RainstrapChangelog = release.Body ?? "";
                }

                // Cache the result
                App.State.Prop.LastRainstrapCheckUtc = DateTime.UtcNow;
                App.State.Prop.LastRainstrapReleaseTag = release.TagName;
                App.State.Prop.LastRainstrapReleaseBody = release.Body;
                App.State.Save();

                RainstrapLastCheckedText = String.Format(Strings.Updates_LastChecked, DateTime.UtcNow.ToLocalTime().ToString("g"));

                App.Logger.WriteLine(LOG_IDENT, $"Rainstrap version check: current={App.Version}, latest={release.TagName}");
            }
            catch (TaskCanceledException)
            {
                RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, "Request timed out");
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"HTTP error: {ex.Message}");
                RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, ex.Message);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, ex.Message);
            }
            finally
            {
                IsCheckingRainstrap = false;
            }
        }

        private async Task InstallRainstrapUpdateAsync()
        {
            const string LOG_IDENT = "UpdatesViewModel::InstallRainstrapUpdate";

            if (_latestRainstrapRelease is null || _isInstallingRainstrap)
                return;

            var asset = _latestRainstrapRelease.Assets?.FirstOrDefault();
            if (asset is null)
            {
                RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, "No download asset found");
                return;
            }

            MessageBoxResult choice = Frontend.ShowMessageBox(
                String.Format(Strings.Updates_Rainstrap_UpdateAvailable, _latestRainstrapRelease.TagName),
                MessageBoxImage.Question,
                MessageBoxButton.YesNo,
                MessageBoxResult.Yes
            );

            if (choice != MessageBoxResult.Yes)
                return;

            IsInstallingRainstrap = true;
            RainstrapError = "";

            try
            {
                RainstrapStatusText = Strings.Updates_Rainstrap_Downloading;

                string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                if (!File.Exists(downloadLocation))
                {
                    var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(downloadLocation, FileMode.OpenOrCreate, FileAccess.Write);
                    await response.Content.CopyToAsync(fileStream);
                }

                App.Logger.WriteLine(LOG_IDENT, $"Downloaded {_latestRainstrapRelease.TagName} to {downloadLocation}");

                RainstrapStatusText = Strings.Updates_Rainstrap_Installing;

                ProcessStartInfo startInfo = new()
                {
                    FileName = downloadLocation,
                    Arguments = "-upgrade"
                };

                App.Settings.Save();
                Process.Start(startInfo);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                RainstrapError = String.Format(Strings.Updates_Rainstrap_CheckFailed, ex.Message);
                RainstrapStatusText = "";
            }
            finally
            {
                IsInstallingRainstrap = false;
            }
        }
    }
}
