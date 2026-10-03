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

        /// <summary>
        /// The result currently on screen. Seeded from disk in the constructor, so
        /// reopening the tab - or restarting Rainstrap - shows the last real answer
        /// rather than resetting to "not checked".
        /// </summary>
        private ClientVersion? _cachedClientVersion;

        private DateTime? _lastRobloxCheck = null;
        private long _robloxApiLatencyMs = -1;

        /// <summary>
        /// True when what is on screen came from disk rather than from a check run in
        /// this session. The UI says so, so a saved result is never mistaken for a
        /// freshly completed one.
        /// </summary>
        private bool _isRestoredFromDisk = false;

        public UpdatesViewModel()
        {
            UpgradeModes = new Dictionary<UpgradeMode, string>
            {
                { UpgradeMode.Automatic, Strings.Updates_UpgradeMode_Automatic },
                { UpgradeMode.Notify, Strings.Updates_UpgradeMode_Notify }
            };

            SelectedUpgradeMode = App.Settings.Prop.UpgradeMode;

            RestoreLastCheck();

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

        /// <summary>
        /// Whether the displayed result was restored from disk rather than produced by a
        /// check in this session. Backs the "previously checked" label so a saved result
        /// is never presented as a live one.
        /// </summary>
        public bool IsRestoredResult => _isRestoredFromDisk && !_isCheckingRoblox;

        public string RestoredBadgeVisibility => IsRestoredResult ? "Visible" : "Collapsed";

        public string RestoredBadgeText =>
            string.Format(Strings.Updates_RestoredResult, _lastRobloxCheck?.ToLocalTime().ToString("g") ?? "");

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
                if (_isCheckingRoblox)
                    return Strings.Updates_Checking;

                if (_cachedClientVersion == null)
                    return Strings.Updates_NotChecked;

                // A restored result is prefixed rather than replaced, so the title
                // states both the answer and that it is not a live check.
                var status = ComputeUpdateStatus();

                return IsRestoredResult
                    ? string.Format(Strings.Updates_RestoredStatusTitle, status)
                    : status;
            }
        }

        public string StatusDescription
        {
            get
            {
                if (_isCheckingRoblox)
                    return Strings.Updates_CheckingDescription;

                if (_cachedClientVersion == null)
                    return Strings.Updates_NotChecked;

                var currentVersion = GetCurrentRobloxVersion();
                if (currentVersion == null)
                    return Strings.Updates_NotChecked;

                var comparison = Utilities.CompareVersions(currentVersion.ToString(), _cachedClientVersion.Version);

                var description = comparison == VersionComparison.LessThan
                    ? string.Format(Strings.Updates_UpdateAvailableDescription, _cachedClientVersion.Version)
                    : Strings.Updates_UpToDateDescription;

                if (IsRestoredResult)
                    return string.Format(Strings.Updates_RestoredStatusDescription, description);

                return description;
            }
        }

        /// <summary>
        /// The actual answer, with no "previously checked" framing applied.
        /// Kept separate so StatusTitle can wrap it once instead of duplicating
        /// the comparison logic.
        /// </summary>
        private string ComputeUpdateStatus()
        {
            if (_cachedClientVersion == null)
                return Strings.Updates_NotChecked;

            // Compare Roblox versions only. This previously also compared
            // App.Version (Rainstrap's own build number) against the Roblox
            // version, which is meaningless - a Roblox build of 0.7xx is
            // numerically "greater than" any Rainstrap 1.x release, so that
            // branch could report an update to the user on any machine.
            var currentVersion = GetCurrentRobloxVersion();
            if (currentVersion == null)
                return Strings.Updates_NotChecked;

            var comparison = Utilities.CompareVersions(currentVersion.ToString(), _cachedClientVersion.Version);
            return comparison == VersionComparison.LessThan ? Strings.Updates_UpdateAvailable : Strings.Updates_UpToDate;
        }

        /// <summary>
        /// The version the user pinned in Version Control, or null when they
        /// are following the channel.
        ///
        /// Read live rather than cached so the Updates tab cannot show a stale
        /// answer after a selection change on the Version Control page.
        /// </summary>
        private string? GetSelectedVersionGuid()
        {
            string selected = VersionControl.SelectedVersionGuid;

            return String.IsNullOrEmpty(selected) ? null : selected;
        }

        private Version? GetCurrentRobloxVersion()
        {
            // The selected version is what will actually be launched, so that -
            // not whatever happens to be installed - is what an update check has
            // to compare against. Otherwise an older pinned version makes this tab
            // claim the install is current when it is deliberately not.
            string? versionGuid = GetSelectedVersionGuid()
                ?? App.RobloxState.Prop.Player.VersionGuid;

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

        // CanExecute rather than a bare RelayCommand: without it the button is only
        // disabled by a property change racing the click, which lets a double-click
        // start two overlapping checks.
        private AsyncRelayCommand? _checkForUpdatesCommand;

        public ICommand CheckForUpdatesCommand =>
            _checkForUpdatesCommand ??= new AsyncRelayCommand(CheckForRobloxUpdatesAsync, () => !_isCheckingRoblox);

        #endregion

        #region Public Methods

        public void RefreshCurrentVersion()
        {
            var history = App.RobloxState.Prop.PlayerVersionHistory;

            // keep the items we already have - rebuilding them is pointless churn
            // and this runs again every time the page is loaded
            for (int i = RecentVersions.Count - 1; i >= 0; i--)
            {
                if (!history.Any(x => x.VersionGuid == RecentVersions[i].Guid))
                    RecentVersions.RemoveAt(i);
            }

            foreach (var entry in history)
            {
                if (RecentVersions.Any(x => x.Guid == entry.VersionGuid))
                    continue;

                RecentVersions.Add(new VersionHistoryItemViewModel(entry, this));
            }

            OnPropertyChanged(nameof(CurrentVersionText));
            OnPropertyChanged(nameof(LastCheckedText));
            OnPropertyChanged(nameof(HistoryEmptyVisibility));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads the last successful check from disk.
        ///
        /// The decision rules live in <see cref="UpdateCheckPersistence"/> so they
        /// can be tested without a window or a Roblox connection. A record from a
        /// different channel is discarded rather than shown: a version number for
        /// "production" says nothing about the channel the user is actually on, and
        /// presenting it as if it did would be worse than showing nothing.
        /// </summary>
        private void RestoreLastCheck()
        {
            var saved = App.State.Prop.LastUpdateCheck;

            var decision = UpdateCheckPersistence.Evaluate(saved, Deployment.Channel);

            // Bound to a local first: deconstruction does not carry the non-null
            // narrowing into the rest of the method, so the checks below would each
            // have to re-assert it.
            var restored = decision.Restored;
            if (restored is null)
            {
                if (decision.Outcome == RestoredCheckOutcome.WrongChannel)
                    App.Logger.WriteLine(
                        "UpdatesViewModel",
                        $"Ignoring saved update check for channel '{saved!.Channel}'; current channel is '{Deployment.Channel}'");
                return;
            }

            _cachedClientVersion = new ClientVersion
            {
                // Evaluate() only returns a record whose Version is non-empty, so
                // this is the one place the nullability has to be asserted.
                Version = restored.Version!,
                VersionGuid = restored.VersionGuid ?? "",
                BootstrapperVersion = "",
                Timestamp = restored.PublishedUtc,
            };

            _lastRobloxCheck = restored.CheckedUtc;

            _robloxApiLatencyMs = restored.LatencyMs;
            _isRestoredFromDisk = true;

            App.Logger.WriteLine(
                "UpdatesViewModel",
                $"Restored last update check: version {restored.Version} from {_lastRobloxCheck:u}");
        }

        /// <summary>
        /// Persists a successful check.
        ///
        /// Only called on success, which is what makes the result survive closing the
        /// tab and restarting Rainstrap. This is the only writer of
        /// <see cref="State.LastUpdateCheck"/>, so the saved state and the displayed
        /// state cannot diverge.
        /// </summary>
        private void PersistCheck(ClientVersion version, long latencyMs)
        {
            if (!UpdateCheckPersistence.ShouldPersist(true, version.Version))
                return;

            var now = DateTime.UtcNow;

            App.State.Prop.LastUpdateCheck = UpdateCheckPersistence.Build(
                Deployment.Channel,
                version.Version,
                version.VersionGuid,
                version.Timestamp,
                now,
                latencyMs);

            // LastUpdateCheckUtc predates LastUpdateCheck and is kept in step so any
            // existing consumer of it keeps working.
            App.State.Prop.LastUpdateCheckUtc = now;

            App.State.Save();
        }

        private async Task CheckForRobloxUpdatesAsync()
        {
            // The relay command's CanExecute already guards this, but the guard is
            // repeated here because it is the thing that stops two overlapping checks
            // racing to write State.json.
            if (_isCheckingRoblox)
                return;

            _isCheckingRoblox = true;
            _robloxError = null;
            OnPropertyChanged(nameof(CheckButtonText));
            OnPropertyChanged(nameof(CheckNowEnabled));
            OnPropertyChanged(nameof(ErrorVisibility));
            OnPropertyChanged(nameof(StatusTitle));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(IsRestoredResult));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var version = await Deployment.GetInfo(Deployment.Channel, behindProductionCheck: true, includeTimestamp: true);
                stopwatch.Stop();
                _robloxApiLatencyMs = stopwatch.ElapsedMilliseconds;

                _cachedClientVersion = version;
                _lastRobloxCheck = DateTime.UtcNow;
                // A check completed in this session, so the "previously checked"
                // framing no longer applies.
                _isRestoredFromDisk = false;

                PersistCheck(version, _robloxApiLatencyMs);

                OnPropertyChanged(nameof(AvailableVersionText));
                OnPropertyChanged(nameof(CurrentVersionText));
                OnPropertyChanged(nameof(LastCheckedText));
                OnPropertyChanged(nameof(ApiLatencyText));
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(IsRestoredResult));
                OnPropertyChanged(nameof(RestoredBadgeVisibility));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _robloxApiLatencyMs = stopwatch.ElapsedMilliseconds;
                _robloxError = string.Format(Strings.Updates_CheckFailed, ex.Message);

                // Deliberately does NOT clear _cachedClientVersion. A failed check
                // must not destroy the last known good result: the user still wants
                // to know what the last successful answer was, and the error is shown
                // alongside it rather than in place of it.
                App.Logger.WriteLine("UpdatesViewModel", $"Update check failed, keeping previous result: {ex.Message}");

                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(ErrorVisibility));
            }
            finally
            {
                _isCheckingRoblox = false;
                OnPropertyChanged(nameof(CheckButtonText));
                OnPropertyChanged(nameof(CheckNowEnabled));
                OnPropertyChanged(nameof(StatusTitle));
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(IsRestoredResult));
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

        /// <summary>
        /// A read-only record of a version this device installed at some point.
        ///
        /// Deliberately has no rollback action any more. Selecting a version is
        /// Version Control's job now, and it does it through a persisted selection
        /// rather than by relaunching with a command line argument - so this list
        /// says what happened here without offering a second, competing way to
        /// change what launches. A record in this list is also not evidence that
        /// Roblox still publishes the version; Version Control is what verifies
        /// that.
        /// </summary>
        public class VersionHistoryItemViewModel : NotifyPropertyChangedViewModel
        {
            private readonly PlayerVersionHistoryEntry _entry;
            private readonly UpdatesViewModel _parent;

            public VersionHistoryItemViewModel(PlayerVersionHistoryEntry entry, UpdatesViewModel parent)
            {
                _entry = entry;
                _parent = parent;
            }

            public string Guid => _entry.VersionGuid;

            public string DisplayVersion =>
                !string.IsNullOrEmpty(_entry.Version) ? _entry.Version : _entry.VersionGuid[..Math.Min(8, _entry.VersionGuid.Length)];

            public string VersionGuid => _entry.VersionGuid;

            public string InstalledText =>
                string.Format(Strings.Updates_History_InstalledOn, _entry.InstalledAtUtc.ToLocalTime().ToString("g"));

            public string StateBadgeText => State switch
            {
                VersionHistoryRules.VersionState.Current => Strings.Updates_History_BadgeCurrent,
                VersionHistoryRules.VersionState.Latest => Strings.Updates_History_BadgeLatest,
                _ => Strings.Updates_History_BadgePrevious,
            };

            /// <summary>
            /// The version's state relative to current and latest. Classified by
            /// <see cref="VersionHistoryRules"/> rather than inline, so the badge and
            /// any eligibility rule cannot drift apart. Current wins over latest, so a
            /// version that is both installed and newest is not badged twice.
            /// </summary>
            public VersionHistoryRules.VersionState State =>
                VersionHistoryRules.Classify(
                    _entry.VersionGuid,
                    App.RobloxState.Prop.Player.VersionGuid,
                    _parent._cachedClientVersion?.VersionGuid);

            /// <summary>True when this is the version currently installed on disk.</summary>
            public bool IsCurrent => State == VersionHistoryRules.VersionState.Current;

            /// <summary>True when this is the version the channel currently publishes.</summary>
            public bool IsLatest => State == VersionHistoryRules.VersionState.Latest;

            /// <summary>True when Version Control has this version pinned.</summary>
            public bool IsSelected =>
                String.Equals(_entry.VersionGuid, VersionControl.SelectedVersionGuid, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
