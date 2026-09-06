using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Plugins;

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
        private readonly MultiInstancePlugin _plugin;

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
                _plugin.SetMultiInstanceLaunching(value);
                OnPropertyChanged(nameof(MultiInstanceLaunchingEnabled));
            }
        }

        public ICommand RefreshCommand => new RelayCommand(Refresh);

        public ICommand LaunchAnotherCommand => new AsyncRelayCommand(async () =>
        {
            if (_isLaunching)
                return;

            IsLaunching = true;

            try
            {
                bool launched = _plugin.TryLaunchInstance();

                if (launched)
                    await Task.Delay(1500);
            }
            finally
            {
                IsLaunching = false;
                Refresh();
            }
        });

        public MultiInstanceViewModel(MultiInstancePlugin plugin)
        {
            _plugin = plugin;
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
