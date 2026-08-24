using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Enums;
using Bloxstrap.Models.APIs.Roblox;
using Bloxstrap.RobloxInterfaces;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class UpdatesViewModel : NotifyPropertyChangedViewModel
    {
        private ClientVersion? _availableVersion;

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

        public UpdatesViewModel()
        {
            RefreshCurrentVersion();
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
                ClientVersion clientVersion = await Deployment.GetInfo(Deployment.DefaultChannel);

                _availableVersion = clientVersion;

                double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
                ApiLatencyText = String.Format(Strings.Updates_ApiLatency, (int)Math.Round(elapsedMs));

                App.State.Prop.LastUpdateCheckUtc = DateTime.UtcNow;
                App.State.Save();

                ApplyVerdict(clientVersion);
                OnPropertyChanged(nameof(LastCheckedText));
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
    }
}
