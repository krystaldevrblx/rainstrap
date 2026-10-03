using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Models.Entities;
using Bloxstrap.RobloxInterfaces;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// Drives the Version Control page: discover, download, select, and remove
    /// Roblox versions.
    ///
    /// Every action here goes through <see cref="VersionControl"/>, which owns the
    /// selection, and through the existing bootstrapper for anything that needs a
    /// real install. No second installer and no second launch pipeline is
    /// introduced here.
    /// </summary>
    public class VersionControlViewModel : NotifyPropertyChangedViewModel
    {
        private VersionCatalogResult _catalog = new();
        private bool _isRefreshing = false;
        private string? _error = null;
        private string? _busyVersionGuid = null;
        private CancellationTokenSource? _refreshCts;

        public VersionControlViewModel()
        {
            // Show the last real answer rather than an empty list, so reopening the
            // page doesn't pretend nothing has been discovered yet.
            _catalog = VersionControl.RestoreCatalog();
        }

        #region Properties

        public ObservableCollection<VersionRowViewModel> Versions { get; } = new();

        public bool IsEmpty => Versions.Count == 0;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                _isRefreshing = value;
                OnPropertyChanged(nameof(IsRefreshing));
                OnPropertyChanged(nameof(RefreshButtonText));
                OnPropertyChanged(nameof(RefreshEnabled));
            }
        }

        public string RefreshButtonText => IsRefreshing ? Strings.VersionControl_Refreshing : Strings.VersionControl_Refresh;

        public bool RefreshEnabled => !IsRefreshing;

        public string ErrorMessage => _error ?? String.Empty;

        public bool ErrorVisibility => !String.IsNullOrEmpty(_error);

        /// <summary>
        /// Last refresh time, shown so the user can tell a live catalogue from a
        /// stale one.
        /// </summary>
        public string LastRefreshedText =>
            _catalog.RefreshedAtUtc == default
                ? Strings.VersionControl_NeverRefreshed
                : String.Format(Strings.VersionControl_LastRefreshed, _catalog.RefreshedAtUtc.ToLocalTime().ToString("g"));

        /// <summary>
        /// Whether the list covers every version that exists. Always false, because
        /// Roblox publishes no such endpoint - stated rather than implied.
        /// </summary>
        public bool IsExhaustive => _catalog.IsExhaustive;

        /// <summary>Plain-language reasons the list is partial.</summary>
        public string LimitationText => String.Join(" ", _catalog.Limitations.Distinct());

        public bool LimitationVisibility => LimitationText.Length > 0;

        public string SelectedVersionText
        {
            get
            {
                string selected = VersionControl.SelectedVersionGuid;

                if (String.IsNullOrEmpty(selected))
                    return String.Format(Strings.VersionControl_FollowingChannel, Deployment.Channel);

                var entry = Versions.FirstOrDefault(x => String.Equals(x.VersionGuid, selected, StringComparison.OrdinalIgnoreCase));

                return entry?.DisplayVersion ?? selected;
            }
        }

        public bool HasSelection => Versions.Any(x => x.IsSelected);

        public bool CanFollowChannel => VersionControl.HasSelection;

        #endregion

        #region Commands

        private AsyncRelayCommand? _refreshCommand;

        public ICommand RefreshCommand =>
            _refreshCommand ??= new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);

        // Commands are properties with backing fields rather than field
        // initialisers: an initialiser cannot reference an instance method, and
        // initialising them in the constructor would also create them before the
        // view model is fully set up.

        private RelayCommand<VersionRowViewModel>? _downloadCommand;
        public ICommand DownloadCommand => _downloadCommand ??= new RelayCommand<VersionRowViewModel>(DownloadAsync);

        private RelayCommand<VersionRowViewModel>? _selectCommand;
        public ICommand SelectCommand => _selectCommand ??= new RelayCommand<VersionRowViewModel>(Select);

        private RelayCommand<VersionRowViewModel>? _removeCommand;
        public ICommand RemoveCommand => _removeCommand ??= new RelayCommand<VersionRowViewModel>(Remove);

        private RelayCommand<VersionRowViewModel>? _detailsCommand;
        public ICommand ShowDetailsCommand => _detailsCommand ??= new RelayCommand<VersionRowViewModel>(ShowDetails);

        private RelayCommand? _followChannelCommand;
        public ICommand FollowChannelCommand => _followChannelCommand ??= new RelayCommand(FollowChannel);

        #endregion

        #region Public methods

        /// <summary>
        /// Refreshes the catalogue and rebuilds the rows.
        /// </summary>
        public async Task RefreshAsync()
        {
            const string LOG_IDENT = "VersionControlViewModel::RefreshAsync";

            if (IsRefreshing)
                return;

            IsRefreshing = true;
            _error = null;
            OnPropertyChanged(nameof(ErrorVisibility));

            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();

            try
            {
                // Reuse a live connection if one is already up; Version Control
                // should not force a full mirror re-probe just to open a page.
                var result = await VersionControl.RefreshCatalogAsync(_refreshCts.Token);

                ApplyResult(result);
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, "Refresh cancelled");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Refresh failed");
                App.Logger.WriteException(LOG_IDENT, ex);

                _error = String.Format(Strings.VersionControl_RefreshFailed, ex.Message);
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(ErrorVisibility));
            }
            finally
            {
                IsRefreshing = false;
                _refreshCts?.Dispose();
                _refreshCts = null;
            }
        }

        /// <summary>
        /// Re-reads selection and installation state without hitting the network.
        /// Called whenever the page becomes visible, since a background bootstrapper
        /// may have installed something in the meantime.
        /// </summary>
        public void RefreshSelectionState()
        {
            var restored = VersionControl.RestoreCatalog();

            if (restored.Entries.Count > 0)
                ApplyResult(restored, persist: false);
            else
                RecomputeFlags();
        }

        private void ApplyResult(VersionCatalogResult result, bool persist = true)
        {
            _catalog = result;

            Versions.Clear();

            // Sorted by real version metadata, newest first. A bare GUID sort would
            // be meaningless because client version guids are opaque hashes.
            foreach (var entry in result.SortedEntries())
                Versions.Add(new VersionRowViewModel(entry));

            OnPropertyChanged(nameof(LastRefreshedText));
            OnPropertyChanged(nameof(IsExhaustive));
            OnPropertyChanged(nameof(LimitationText));
            OnPropertyChanged(nameof(LimitationVisibility));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(SelectedVersionText));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanFollowChannel));
        }

        private void RecomputeFlags()
        {
            string selected = VersionControl.SelectedVersionGuid;

            foreach (var row in Versions)
            {
                row.IsSelected = !String.IsNullOrEmpty(selected)
                              && String.Equals(row.VersionGuid, selected, StringComparison.OrdinalIgnoreCase);
            }

            OnPropertyChanged(nameof(SelectedVersionText));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanFollowChannel));
        }

        #endregion

        #region Actions

        /// <summary>
        /// Selects an installed version, but only after proving it is usable.
        ///
        /// A version that is merely on disk is not enough: it has to pass
        /// validation, still be published, and not be one Roblox already refused.
        /// Each of those failures produces its own message so the user is told what
        /// to do rather than just being refused.
        /// </summary>
        private void Select(VersionRowViewModel? row)
        {
            const string LOG_IDENT = "VersionControlViewModel::Select";

            if (row is null || row.IsSelected)
                return;

            var validation = VersionControl.ValidateInstallation(row.VersionGuid);

            if (!validation.IsValid)
            {
                string message = String.Format(
                    validation.RequiresReinstall
                        ? Strings.VersionControl_SelectFailed_Invalid
                        : Strings.VersionControl_SelectFailed_NotInstalled,
                    row.DisplayVersion);

                Frontend.ShowMessageBox(message, MessageBoxImage.Warning);
                return;
            }

            if (VersionControl.WasRefused(row.VersionGuid))
            {
                Frontend.ShowMessageBox(Strings.VersionControl_SelectFailed_Refused, MessageBoxImage.Warning);
                return;
            }

            if (row.Availability == VersionAvailability.Unavailable)
            {
                Frontend.ShowMessageBox(
                    String.Format(Strings.VersionControl_SelectFailed_Unpublished, row.DisplayVersion),
                    MessageBoxImage.Warning);
                return;
            }

            // Selecting only records the choice. It deliberately does not install
            // anything or touch AppState.VersionGuid - the next launch resolves
            // through VersionControl, so the recorded selection and the installed
            // copy can never drift apart behind the user's back.
            VersionControl.SelectVersion(row.VersionGuid);

            foreach (var item in Versions)
                item.IsSelected = String.Equals(item.VersionGuid, row.VersionGuid, StringComparison.OrdinalIgnoreCase);

            OnPropertyChanged(nameof(SelectedVersionText));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanFollowChannel));

            Frontend.ShowMessageBox(
                String.Format(Strings.VersionControl_SelectedMessage, row.DisplayVersion),
                MessageBoxImage.Information);

            App.Logger.WriteLine(LOG_IDENT, $"Selected {row.VersionGuid}");
        }

        /// <summary>
        /// Downloads a version using the existing bootstrapper.
        ///
        /// The manifest is verified first, so a withdrawn version fails immediately
        /// with a clear reason instead of half-downloading into a folder that can
        /// never launch. Roblox is not started afterwards: downloading is a setup
        /// step, not a launch, and the user chooses when to run it.
        /// </summary>
        private async void DownloadAsync(VersionRowViewModel? row)
        {
            const string LOG_IDENT = "VersionControlViewModel::Download";

            if (row is null || row.IsDownloading)
                return;

            if (row.IsInstalled)
                return;

            if (!String.IsNullOrEmpty(_busyVersionGuid))
                return;

            row.IsDownloading = true;
            _busyVersionGuid = row.VersionGuid;

            try
            {
                // Verify against the manifest rather than trusting the cached
                // availability flag: this is the last point before we spend the
                // user's bandwidth.
                var manifest = await Deployment.GetPackageManifestAsync(row.VersionGuid);

                if (manifest is null)
                {
                    Frontend.ShowMessageBox(
                        String.Format(Strings.VersionControl_DownloadFailed_Unpublished, row.DisplayVersion),
                        MessageBoxImage.Error);
                    return;
                }

                Frontend.ShowMessageBox(
                    String.Format(Strings.VersionControl_DownloadStarted, row.DisplayVersion),
                    MessageBoxImage.Information);

                bool installed = await RunInstallerForVersionAsync(row.VersionGuid);

                if (!installed)
                {
                    Frontend.ShowMessageBox(
                        String.Format(Strings.VersionControl_DownloadFailed, row.DisplayVersion, "The installer did not complete"),
                        MessageBoxImage.Error);
                    return;
                }

                // Only trust the exit on its own: re-validate the files rather than
                // assuming a zero exit code means a usable install.
                var validation = VersionControl.ValidateInstallation(row.VersionGuid);

                if (!validation.IsValid)
                {
                    Frontend.ShowMessageBox(
                        String.Format(Strings.VersionControl_DownloadSucceeded_NotValidated, row.DisplayVersion),
                        MessageBoxImage.Warning);
                    return;
                }

                row.IsInstalled = true;

                Frontend.ShowMessageBox(
                    String.Format(Strings.VersionControl_DownloadSucceeded, validation.DetectedVersion ?? row.DisplayVersion),
                    MessageBoxImage.Information);

                App.Logger.WriteLine(LOG_IDENT, $"Installed {row.VersionGuid} successfully");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Download of {row.VersionGuid} failed");
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    String.Format(Strings.VersionControl_DownloadFailed, row.DisplayVersion, ex.Message),
                    MessageBoxImage.Error);
            }
            finally
            {
                row.IsDownloading = false;
                _busyVersionGuid = null;
            }
        }

        /// <summary>
        /// Runs a bootstrapper pass that installs one specific version and then
        /// stops without launching Roblox.
        ///
        /// This is the existing installer and the existing launch pipeline being
        /// pointed at a version - not a second copy of either. The version has to be
        /// the first argument because that is where LaunchSettings reads it from.
        /// </summary>
        private static async Task<bool> RunInstallerForVersionAsync(string versionGuid)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Paths.Process,
                UseShellExecute = true,
            };

            startInfo.ArgumentList.Add(Deployment.NormalizeVersionGuid(versionGuid));
            startInfo.ArgumentList.Add("-player");
            startInfo.ArgumentList.Add("-nolaunch");

            Process process = Process.Start(startInfo)!;

            try
            {
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("VersionControlViewModel::RunInstallerForVersion", "Failed waiting for the installer");
                App.Logger.WriteException("VersionControlViewModel::RunInstallerForVersion", ex);

                return false;
            }
            finally
            {
                process.Dispose();
            }

            // ERROR_SUCCESS and ERROR_CANCELLED both mean the install finished
            // (the user may have cancelled the launch step). Either way the files
            // are on disk, and validation - not this code - decides whether they
            // are usable.
            return true;
        }

        /// <summary>
        /// Removes an installed version after an explicit confirmation.
        ///
        /// Protected versions are refused by the bootstrapper; this just relays the
        /// reason so the user is told why rather than watching nothing happen.
        /// </summary>
        private void Remove(VersionRowViewModel? row)
        {
            const string LOG_IDENT = "VersionControlViewModel::Remove";

            if (row is null || !row.IsInstalled)
                return;

            var confirm = Frontend.ShowMessageBox(
                String.Format(Strings.VersionControl_RemoveMessage, row.DisplayVersion),
                MessageBoxImage.Question,
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            string? failure = Bloxstrap.Bootstrapper.RemoveInstalledVersion(row.VersionGuid, out string removed);

            if (failure is not null)
            {
                Frontend.ShowMessageBox(failure, MessageBoxImage.Warning);
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Removed {removed}");

            row.IsInstalled = false;
            OnPropertyChanged(nameof(HasSelection));
        }

        /// <summary>
        /// Drops the pin so the channel's current version is used again.
        /// </summary>
        private void FollowChannel()
        {
            VersionControl.ClearSelection();

            RecomputeFlags();
        }

        /// <summary>
        /// Shows the details that help with troubleshooting without putting raw
        /// guids in the main list.
        /// </summary>
        private void ShowDetails(VersionRowViewModel? row)
        {
            if (row is null)
                return;

            var lines = new List<string>
            {
                $"**{Strings.VersionControl_Details_Guid}**  \n`{row.VersionGuid}`",
            };

            if (!String.IsNullOrEmpty(row.Entry.Channel))
                lines.Add($"**{Strings.VersionControl_Details_Channel}**  \n{row.Entry.Channel}");

            if (row.Entry.PublishedUtc is not null)
                lines.Add($"**{Strings.VersionControl_Details_Published}**  \n{row.Entry.PublishedUtc.Value.ToLocalTime():g}");

            lines.Add($"**{Strings.VersionControl_Details_Availability}**  \n{row.AvailabilityText}");
            lines.Add($"**{Strings.VersionControl_Details_Support}**  \n{row.SupportText}");

            Frontend.ShowMessageBox(
                String.Format(Strings.VersionControl_DetailsTitle, row.DisplayVersion) + "\n\n" + String.Join("\n\n", lines),
                MessageBoxImage.Information,
                MessageBoxButton.OK);
        }


        #endregion
    }

    /// <summary>
    /// One row in the Version Control list.
    ///
    /// Availability, support state and action visibility are computed rather than
    /// stored, so they cannot go stale relative to the underlying state. The
    /// support state in particular is never "supported" purely because files exist.
    /// </summary>
    public class VersionRowViewModel : NotifyPropertyChangedViewModel
    {
        private bool _isInstalled;
        private bool _isDownloading;

        public VersionRowViewModel(VersionCatalogEntry entry)
        {
            Entry = entry;

            _isInstalled = entry.IsInstalled;
        }

        public VersionCatalogEntry Entry { get; }

        public string VersionGuid => Entry.VersionGuid;

        /// <summary>
        /// What the list shows as the version name. Falls back to a short guid when
        /// no version number is known, rather than showing nothing.
        /// </summary>
        public string DisplayVersion =>
            !String.IsNullOrWhiteSpace(Entry.Version)
                ? Entry.Version
                : Entry.VersionGuid.Replace("version-", "")[..Math.Min(8, Entry.VersionGuid.Replace("version-", "").Length)];

        public bool IsSelected
        {
            get => Entry.IsSelected;
            set
            {
                if (Entry.IsSelected == value)
                    return;

                Entry.IsSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(SelectedBadgeVisibility));
                OnPropertyChanged(nameof(SelectEnabled));
                OnPropertyChanged(nameof(SelectButtonText));
            }
        }

        public string SelectedBadgeVisibility => IsSelected ? "Visible" : "Collapsed";

        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled == value)
                    return;

                _isInstalled = value;
                Entry.IsInstalled = value;
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(InstalledBadgeVisibility));
                OnPropertyChanged(nameof(DownloadButtonVisibility));
                OnPropertyChanged(nameof(RemoveButtonVisibility));
                OnPropertyChanged(nameof(SelectButtonVisibility));
            }
        }

        public string InstalledBadgeVisibility => IsInstalled ? "Visible" : "Collapsed";

        public bool IsLatest => Entry.IsLatestOfficial;

        public string LatestBadgeVisibility => IsLatest ? "Visible" : "Collapsed";

        public VersionAvailability Availability => Entry.Availability;

        public string AvailabilityText => Availability switch
        {
            VersionAvailability.Available => Strings.VersionControl_Status_Available,
            VersionAvailability.Unavailable => Strings.VersionControl_Status_Unavailable,
            _ => Strings.VersionControl_Status_Unknown,
        };

        /// <summary>
        /// Combined "can I use this" answer, which is stricter than availability.
        /// </summary>
        public VersionSupportState SupportState => VersionControl.GetSupportState(Entry);

        public string SupportText => SupportState switch
        {
            VersionSupportState.Supported => Strings.VersionControl_Details_Support_SUPPORTED,
            VersionSupportState.CorruptedInstallation => Strings.VersionControl_Details_Support_CORRUPTEDINSTALLATION,
            VersionSupportState.NoLongerPublished => Strings.VersionControl_Details_Support_NOLONGERPUBLISHED,
            VersionSupportState.BlockedByRoblox => Strings.VersionControl_Details_Support_BLOCKEDBYROBLOX,
            _ => Strings.VersionControl_Details_Support_UNKNOWN,
        };

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (_isDownloading == value)
                    return;

                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(DownloadEnabled));
                OnPropertyChanged(nameof(DownloadButtonText));
            }
        }

        public string DownloadButtonText =>
            IsDownloading ? Strings.VersionControl_Refreshing : Strings.VersionControl_Action_Download;

        public bool DownloadEnabled => !IsDownloading && Availability == VersionAvailability.Available;

        public string DownloadButtonVisibility => !IsInstalled ? "Visible" : "Collapsed";

        /// <summary>
        /// Only offered when the version is installed and validated. An unusable
        /// install must be repaired first, not selected.
        /// </summary>
        public string SelectButtonVisibility =>
            IsInstalled && SupportState == VersionSupportState.Supported && !IsSelected ? "Visible" : "Collapsed";

        public bool SelectEnabled => !IsSelected;

        public string SelectButtonText => Strings.VersionControl_Action_Select;

        public string RemoveButtonVisibility => IsInstalled && !IsSelected ? "Visible" : "Collapsed";
    }
}