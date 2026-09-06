using Bloxstrap.Integrations.Clips;
using Bloxstrap.Plugins;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ClipsViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ClipsManager _clipsManager;
        private readonly ClipsPlugin _plugin;

        public ICommand SaveClipCommand => new AsyncRelayCommand(SaveClip);
        public ICommand OpenClipsFolderCommand => new RelayCommand(OpenClipsFolder);
        public ICommand BrowseOutputFolderCommand => new RelayCommand(BrowseOutputFolder);

        public IEnumerable<int> BufferDurations { get; } = new[] { 15, 30, 60, 120 };
        public IEnumerable<int> CaptureFpsOptions { get; } = new[] { 15, 24, 30, 60 };

        public ClipsViewModel(ClipsManager manager, ClipsPlugin plugin)
        {
            _clipsManager = manager;
            _plugin = plugin;

            _clipsManager.StatusChanged += OnStatusChanged;
            _clipsManager.ClipSaved += OnClipSaved;
            _clipsManager.ErrorOccurred += OnErrorOccurred;
        }

        public bool ClipsEnabled
        {
            get => App.Settings.Prop.ClipsEnabled;
            set
            {
                App.Settings.Prop.ClipsEnabled = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(ClipsEnabled));
                OnPropertyChanged(nameof(CanSaveClip));

                if (value)
                    _plugin.StartCapturing();
                else
                    _plugin.StopCapturing();
            }
        }

        public string SelectedDuration
        {
            get => App.Settings.Prop.ClipsBufferDurationSeconds.ToString();
            set
            {
                if (int.TryParse(value, out int seconds))
                {
                    App.Settings.Prop.ClipsBufferDurationSeconds = seconds;
                    _clipsManager.BufferDurationSeconds = seconds;
                    App.Settings.Save();
                }
            }
        }

        public string SelectedFps
        {
            get => App.Settings.Prop.ClipsCaptureFps.ToString();
            set
            {
                if (int.TryParse(value, out int fps))
                {
                    App.Settings.Prop.ClipsCaptureFps = fps;
                    _clipsManager.CaptureFps = fps;
                    App.Settings.Save();
                }
            }
        }

        public string HotkeyText
        {
            get => App.Settings.Prop.ClipsHotkeyVirtualKey == 0 ? "None" : KeyInterop.KeyFromVirtualKey(App.Settings.Prop.ClipsHotkeyVirtualKey).ToString();
            set
            {
                if (String.IsNullOrEmpty(value) || value == "None")
                {
                    App.Settings.Prop.ClipsHotkeyVirtualKey = 0;
                }
                else
                {
                    try
                    {
                        Key key = (Key)Enum.Parse(typeof(Key), value, true);
                        App.Settings.Prop.ClipsHotkeyVirtualKey = KeyInterop.VirtualKeyFromKey(key);
                    }
                    catch { }
                }
                _clipsManager.UpdateHotkey(App.Settings.Prop.ClipsHotkeyVirtualKey);
                App.Settings.Save();
                OnPropertyChanged(nameof(HotkeyText));
            }
        }

        public string OutputFolder
        {
            get => App.Settings.Prop.ClipsOutputFolder;
            set
            {
                App.Settings.Prop.ClipsOutputFolder = value;
                _clipsManager.OutputFolder = value;
                App.Settings.Save();
                OnPropertyChanged(nameof(OutputFolder));
            }
        }

        private string _statusText = "Idle - Waiting for Roblox to start";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        private string _statusDetail = "Enable replay buffer and launch Roblox to start capturing.";
        public string StatusDetail
        {
            get => _statusDetail;
            set { _statusDetail = value; OnPropertyChanged(nameof(StatusDetail)); }
        }

        public bool CanSaveClip => App.Settings.Prop.ClipsEnabled && _clipsManager.Status != ClipsManager.ClipsStatus.Idle;

        private async Task SaveClip()
        {
            await _plugin.SaveClip();
        }

        private void OpenClipsFolder()
        {
            _plugin.OpenClipsFolder();
        }

        private void BrowseOutputFolder()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = OutputFolder
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                OutputFolder = dialog.SelectedPath;
        }

        private void OnStatusChanged(object? sender, ClipsManager.ClipsStatus status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = status switch
                {
                    ClipsManager.ClipsStatus.Idle => Strings.Clips_Status_Idle,
                    ClipsManager.ClipsStatus.Buffering => Strings.Clips_Status_Buffering,
                    ClipsManager.ClipsStatus.Saving => Strings.Clips_Status_Saving,
                    ClipsManager.ClipsStatus.Saved => Strings.Clips_Status_Saved,
                    ClipsManager.ClipsStatus.Error => string.Format(Strings.Clips_Status_Error, _clipsManager.LastError),
                    _ => "Unknown"
                };

                StatusDetail = status switch
                {
                    ClipsManager.ClipsStatus.Buffering => $"Buffered: {_clipsManager.BufferedFrameCount}/{_clipsManager.MaxFrameCount} frames",
                    ClipsManager.ClipsStatus.Saving => "Encoding and saving clip...",
                    ClipsManager.ClipsStatus.Saved => $"Clip saved to {OutputFolder}",
                    ClipsManager.ClipsStatus.Error => _clipsManager.LastError,
                    _ => ""
                };

                OnPropertyChanged(nameof(CanSaveClip));
            });
        }

        private void OnClipSaved(object? sender, string path)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusDetail = $"Clip saved: {path}";
            });
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusDetail = $"Error: {error}";
            });
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(ClipsEnabled));
            OnPropertyChanged(nameof(SelectedDuration));
            OnPropertyChanged(nameof(SelectedFps));
            OnPropertyChanged(nameof(HotkeyText));
            OnPropertyChanged(nameof(OutputFolder));
            OnPropertyChanged(nameof(CanSaveClip));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusDetail));
        }

        public void Cleanup()
        {
            _clipsManager.StatusChanged -= OnStatusChanged;
            _clipsManager.ClipSaved -= OnClipSaved;
            _clipsManager.ErrorOccurred -= OnErrorOccurred;
        }
    }
}
