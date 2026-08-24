using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class InstanceCardViewModel : NotifyPropertyChangedViewModel
    {
        public int Pid { get; set; }

        public string Username { get; set; } = "";

        public string AccountLabel => String.IsNullOrEmpty(Username) ? Strings.MultiInstance_UnknownAccount : Username;

        public string StartedAtText { get; set; } = "";

        public string StatusText => Strings.MultiInstance_StatusRunning;
    }

    public class MultiInstanceViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<InstanceCardViewModel> Instances { get; } = new();

        private Visibility _noInstancesVisibility = Visibility.Visible;
        public Visibility NoInstancesVisibility
        {
            get => _noInstancesVisibility;
            set
            {
                _noInstancesVisibility = value;
                OnPropertyChanged(nameof(NoInstancesVisibility));
            }
        }

        private bool _isLaunching = false;
        public bool IsLaunching
        {
            get => _isLaunching;
            private set
            {
                _isLaunching = value;
                OnPropertyChanged(nameof(IsLaunching));
                OnPropertyChanged(nameof(LaunchButtonsEnabled));
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }

        public string LaunchButtonText => IsLaunching ? Strings.MultiInstance_StatusLaunching : Strings.MultiInstance_LaunchAnother;

        public bool LaunchButtonsEnabled => !_isLaunching;

        public bool MultiInstanceLaunchingEnabled
        {
            get => App.Settings.Prop.MultiInstanceLaunching;
            set
            {
                App.Settings.Prop.MultiInstanceLaunching = value;
                App.Settings.Save();
            }
        }

        public ICommand RefreshCommand => new RelayCommand(Refresh);

        public ICommand LaunchAnotherCommand => new AsyncRelayCommand(async () =>
        {
            if (_isLaunching)
                return;

            // guard against accidental parallel launches of the launcher itself
            if (Utilities.IsRobloxRunning() && !App.Settings.Prop.MultiInstanceLaunching)
            {
                var choice = Frontend.ShowMessageBox(
                    Strings.MultiInstance_ConfirmParallel,
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo,
                    MessageBoxResult.No
                );

                if (choice != MessageBoxResult.Yes)
                    return;
            }

            IsLaunching = true;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = "-player",
                    UseShellExecute = false
                });

                await Task.Delay(1500);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"{Strings.Accounts_LaunchFailed}\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                IsLaunching = false;
                Refresh();
            }
        });

        public MultiInstanceViewModel()
        {
            Refresh();
        }

        /// <summary>
        /// Rebuilds the instance list from application state, dropping entries whose
        /// processes are no longer running. The OS process list is the source of truth.
        /// </summary>
        public void Refresh()
        {
            Instances.Clear();

            try
            {
                string playerProcessName = Path.GetFileNameWithoutExtension(App.RobloxPlayerAppName);
                var alivePids = Utilities.GetProcessesSafe()
                    .Where(x => x.ProcessName == playerProcessName)
                    .Select(x => x.Id)
                    .ToHashSet();

                var entries = App.State.Prop.Instances.Where(x => alivePids.Contains(x.Pid)).ToList();

                int removed = App.State.Prop.Instances.RemoveAll(x => !alivePids.Contains(x.Pid));

                foreach (var entry in entries)
                {
                    Instances.Add(new InstanceCardViewModel
                    {
                        Pid = entry.Pid,
                        Username = entry.Username,
                        StartedAtText = String.Format(Strings.MultiInstance_StartedAt, entry.StartedAtUtc.ToLocalTime().ToString("g"))
                    });
                }

                NoInstancesVisibility = Instances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                if (removed > 0)
                    App.State.Save();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("MultiInstanceViewModel::Refresh", $"Failed to refresh instances: {ex.Message}");
            }
        }
    }
}
