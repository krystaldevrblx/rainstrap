using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Models.Persistable;
using Bloxstrap.RobloxInterfaces;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class UpdatesViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isCheckingRoblox = false;
        private string? _robloxError = null;

        private ClientVersion? _cachedClientVersion;
        private DateTime? _lastRobloxCheck = null;
        private long _robloxApiLatencyMs = -1;

        public UpdatesViewModel()
        {
            UpgradeModes = new Dictionary<UpgradeMode, string>
            {
                { UpgradeMode.Automatic, Strings.Updates_UpgradeMode_Automatic },
                { UpgradeMode.Notify, Strings.Updates_UpgradeMode_Notify }
            };

            SelectedUpgradeMode = App.Settings.Prop.UpgradeMode;

            RefreshCurrentVersion();
        }

        #region Roblox Update Properties

        public string CurrentVersionText => 
            !string.IsNullOrEmpty(App.RobloxState.Prop.Player.VersionGuid) 
                ? GetVersionStringFromGuid(App.RobloxState.Prop.Player.VersionGuid)
                : Strings.Updates_NotChecked;

        public string AvailableVersionText => 
            _cachedClientVersion?.Version ?? Strings.Updates_NotChecked;

        public string LastCheckedText => 
            _lastRobloxCheck.HasValue 
                ? string.Format(Strings.Updates_LastChecked, _lastRobloxCheck.Value.ToLocalTime().ToString("g"))
                : Strings.Updates_LatencyNotMeasured;

        public string ApiLatencyText => 
            _robloxApiLatencyMs >= 0 
                ? string.Format(Strings.Updates_ApiLatency, _robloxApiLatencyMs)
                : Strings.Updates_LatencyNotMeasured;

        public string ChannelText => 
            string.Format(Strings.Updates_CurrentChannel, Deployment.Channel);

        public string StatusTitle
        {
            get
            {
                if (_cachedClientVersion == null)
                    return Strings.Updates_NotChecked;

                if (Utilities.CompareVersions(App.Version, _cachedClientVersion.Version) == VersionComparison.GreaterThan)
                    return Strings.Updates_UpToDate;

                var currentVersion = GetCurrentRobloxVersion();
                if (currentVersion == null)
                    return Strings.Updates_NotChecked;

                var comparison = Utilities.CompareVersions(currentVersion.ToString(), _cachedClientVersion.Version);
                return comparison == VersionComparison.LessThan ? Strings.Updates_UpdateAvailable : Strings.Updates_UpToDate;
            }
        }

        public string StatusDescription
        {
            get
            {
                if (_cachedClientVersion == null)
                    return Strings.Updates_NotChecked;

                var currentVersion = GetCurrentRobloxVersion();
                if (currentVersion == null)
                    return Strings.Updates_NotChecked;

                var comparison = Utilities.CompareVersions(currentVersion.ToString(), _cachedClientVersion.Version);
                
                if (comparison == VersionComparison.LessThan)
                    return string.Format(Strings.Updates_UpdateAvailableDescription, _cachedClientVersion.Version);
                
                return Strings.Updates_UpToDateDescription;
            }
        }

        private Version? GetCurrentRobloxVersion()
        {
            var versionGuid = App.RobloxState.Prop.Player.VersionGuid;
            if (string.IsNullOrEmpty(versionGuid))
                return null;

            string exePath = Path.Combine(Paths.Versions, versionGuid, "RobloxPlayerBeta.exe");
            if (!File.Exists(exePath))
                return null;

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                return Utilities.ParseVersionSafe(versionInfo.ProductVersion?.Replace(", ", ".") ?? string.Empty);
            }
            catch
            {
                return null;
            }
        }

        public string CheckButtonText => _isCheckingRoblox ? Strings.Updates_Checking : Strings.Updates_CheckNow;

        public bool CheckNowEnabled => !_isCheckingRoblox;

        public string ErrorMessage => _robloxError ?? string.Empty;
        public bool ErrorVisibility => !string.IsNullOrEmpty(_robloxError);

        public ObservableCollection<VersionHistoryItemViewModel> RecentVersions { get; } = new();

        public bool HistoryEmptyVisibility => RecentVersions.Count == 0;

        #endregion

        #region UpgradeMode

        public Dictionary<UpgradeMode, string> UpgradeModes { get; }

        public UpgradeMode SelectedUpgradeMode
        {
            get => App.Settings.Prop.UpgradeMode;
            set
            {
                if (App.Settings.Prop.UpgradeMode != value)
                {
                    App.Settings.Prop.UpgradeMode = value;
                    App.Settings.Save();
                    OnPropertyChanged(nameof(SelectedUpgradeMode));
                }
            }
        }

        #endregion

        #region Commands

        public ICommand CheckForUpdatesCommand => new RelayCommand(async () => await CheckForRobloxUpdatesAsync());

        #endregion

        #region Public Methods

        public void RefreshCurrentVersion()
        {
            RecentVersions.Clear();

            var history = App.RobloxState.Prop.PlayerVersionHistory;
            foreach (var entry in history)
            {
                RecentVersions.Add(new VersionHistoryItemViewModel(entry, this));
            }

            OnPropertyChanged(nameof(CurrentVersionText));
            OnPropertyChanged(nameof(LastCheckedText));
            OnPropertyChanged(nameof(HistoryEmptyVisibility));
        }

        #endregion

        #region Private Methods

        private async Task CheckForRobloxUpdatesAsync()
        {
            if (_isCheckingRoblox)
                return;

            _isCheckingRoblox = true;
            _robloxError = null;
            OnPropertyChanged(nameof(CheckButtonText));
            OnPropertyChanged(nameof(CheckNowEnabled));
            OnPropertyChanged(nameof(ErrorVisibility));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _cachedClientVersion = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck: true, includeTimestamp: true);
                stopwatch.Stop();
                _robloxApiLatencyMs = stopwatch.ElapsedMilliseconds;

                _lastRobloxCheck = DateTime.UtcNow;
                App.State.Prop.LastUpdateCheckUtc = _lastRobloxCheck.Value;
                App.State.Save();

                OnPropertyChanged(nameof(AvailableVersionText));
                OnPropertyChanged(nameof(LastCheckedText));
                OnPropertyChanged(nameof(ApiLatencyText));
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusDescription));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _robloxApiLatencyMs = stopwatch.ElapsedMilliseconds;
                _robloxError = string.Format(Strings.Updates_CheckFailed, ex.Message);
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(ErrorVisibility));
            }
            finally
            {
                _isCheckingRoblox = false;
                OnPropertyChanged(nameof(CheckButtonText));
                OnPropertyChanged(nameof(CheckNowEnabled));
            }
        }

        private string GetVersionStringFromGuid(string versionGuid)
        {
            if (string.IsNullOrEmpty(versionGuid))
                return Strings.Updates_NotChecked;

            // Check if cached client version matches
            if (_cachedClientVersion != null && _cachedClientVersion.VersionGuid == versionGuid)
                return _cachedClientVersion.Version;

            // Otherwise read from file on disk
            string exePath = Path.Combine(Paths.Versions, versionGuid, "RobloxPlayerBeta.exe");
            if (File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    return versionInfo.ProductVersion?.Replace(", ", ".") ?? versionGuid[..8];
                }
                catch
                {
                    return versionGuid[..8];
                }
            }

            return versionGuid[..8];
        }

        #endregion

        #region Version History Item ViewModel

        public class VersionHistoryItemViewModel : NotifyPropertyChangedViewModel
        {
            private readonly PlayerVersionHistoryEntry _entry;
            private readonly UpdatesViewModel _parent;
            private string _rollbackStatusText = string.Empty;

            public VersionHistoryItemViewModel(PlayerVersionHistoryEntry entry, UpdatesViewModel parent)
            {
                _entry = entry;
                _parent = parent;
                RollbackCommand = new RelayCommand(async () => await RollbackAsync());
            }

            public string DisplayVersion => 
                !string.IsNullOrEmpty(_entry.Version) ? _entry.Version : _entry.VersionGuid[..8];

            public string VersionGuid => _entry.VersionGuid;

            public string InstalledText => 
                string.Format(Strings.Updates_History_InstalledOn, _entry.InstalledAtUtc.ToLocalTime().ToString("g"));

            public string StateBadgeText
            {
                get
                {
                    if (IsCurrent) return Strings.Updates_History_BadgeCurrent;
                    if (IsLatest) return Strings.Updates_History_BadgeLatest;
                    return Strings.Updates_History_BadgePrevious;
                }
            }

            public bool IsCurrent => 
                _entry.VersionGuid == App.RobloxState.Prop.Player.VersionGuid;

            public bool IsLatest =>
                _parent._cachedClientVersion != null && 
                _entry.VersionGuid == _parent._cachedClientVersion.VersionGuid;

            public string RollbackStatusText
            {
                get => _rollbackStatusText;
                set
                {
                    _rollbackStatusText = value;
                    OnPropertyChanged(nameof(RollbackStatusText));
                }
            }

            public bool RollbackProbeVisibility => !IsCurrent && !IsLatest;
            public bool RollbackButtonVisibility => !IsCurrent && !IsLatest && !string.IsNullOrEmpty(_rollbackStatusText);

            public ICommand RollbackCommand { get; }

            private async Task RollbackAsync()
            {
                var confirmMessage = string.Format(Strings.Updates_History_RollbackConfirmText, DisplayVersion);
                var confirmTitle = string.Format(Strings.Updates_History_RollbackConfirmTitle, DisplayVersion);
                
                var result = Frontend.ShowMessageBox(
                    $"{confirmTitle}\n\n{confirmMessage}",
                    MessageBoxImage.Question,
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    return;

                RollbackStatusText = Strings.Updates_History_CheckingRollback;
                OnPropertyChanged(nameof(RollbackProbeVisibility));
                OnPropertyChanged(nameof(RollbackButtonVisibility));

                try
                {
                    var clientVersion = await Deployment.GetInfo(_entry.Channel, behindProductionCheck: false);
                    
                    if (clientVersion == null || clientVersion.VersionGuid != _entry.VersionGuid)
                    {
                        RollbackStatusText = Strings.Updates_History_RollbackUnavailable;
                        OnPropertyChanged(nameof(RollbackButtonVisibility));
                        return;
                    }

                    RollbackStatusText = Strings.Updates_History_RollbackAvailable;
                    OnPropertyChanged(nameof(RollbackButtonVisibility));

                    App.LaunchSettings.VersionFlag.Active = true;
                    App.LaunchSettings.VersionFlag.Data = _entry.VersionGuid;

                    App.State.Prop.ForceReinstall = true;
                    App.State.Save();

                    Frontend.ShowMessageBox(
                        string.Format(Strings.Updates_History_RollbackStarted, DisplayVersion),
                        MessageBoxImage.Information);

                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = Paths.Process,
                        UseShellExecute = true
                    };
                    foreach (string arg in App.LaunchSettings.Args)
                        startInfo.ArgumentList.Add(arg);
                    startInfo.ArgumentList.Add("-player");

                    Process.Start(startInfo);
                    App.Terminate(ErrorCode.ERROR_SUCCESS);
                }
                catch (Exception ex)
                {
                    RollbackStatusText = string.Format(Strings.Updates_History_RollbackFailed, ex.Message);
                    OnPropertyChanged(nameof(RollbackButtonVisibility));
                }
            }
        }

        #endregion
    }
}