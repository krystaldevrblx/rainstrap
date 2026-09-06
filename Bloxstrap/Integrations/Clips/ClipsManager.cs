using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace Bloxstrap.Integrations.Clips
{
    public class ClipsManager : IDisposable
    {
        private ReplayBuffer? _replayBuffer;
        private ScreenCapture? _screenCapture;
        private HotkeyManager? _hotkeyManager;
        private System.Threading.Timer? _captureTimer;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isCapturing;
        private bool _isSaving;
        private bool _disposed;

        private int _robloxProcessId;
        private IntPtr _robloxWindowHandle;

        public enum ClipsStatus
        {
            Idle,
            Buffering,
            Saving,
            Saved,
            Error
        }

        public ClipsStatus Status { get; private set; } = ClipsStatus.Idle;
        public string LastError { get; private set; } = "";
        public int BufferedFrameCount => _replayBuffer?.Count ?? 0;
        public int MaxFrameCount => _replayBuffer?.MaxFrames ?? 0;

        public event EventHandler<ClipsStatus>? StatusChanged;
        public event EventHandler<string>? ClipSaved;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsEnabled { get; private set; }
        public int ClipHotkey { get; set; }
        public int BufferDurationSeconds { get; set; } = 30;
        public int CaptureFps { get; set; } = 30;
        public string OutputFolder { get; set; } = "";

        public void Initialize(IntPtr windowHandle, int processId)
        {
            const string LOG_IDENT = "ClipsManager::Initialize";

            _robloxWindowHandle = windowHandle;
            _robloxProcessId = processId;

            _screenCapture?.Dispose();
            _screenCapture = new ScreenCapture(windowHandle);

            _replayBuffer?.Dispose();
            _replayBuffer = new ReplayBuffer(BufferDurationSeconds, CaptureFps);

            if (string.IsNullOrEmpty(OutputFolder))
                OutputFolder = Path.Combine(Paths.Base, "Clips");

            Directory.CreateDirectory(OutputFolder);

            _hotkeyManager?.Dispose();
            _hotkeyManager = new HotkeyManager(windowHandle);
                _hotkeyManager.HotkeyPressed += async (_, _) => await SaveClip();

            App.Logger.WriteLine(LOG_IDENT, $"Initialized: buffer={BufferDurationSeconds}s, fps={CaptureFps}, output={OutputFolder}");
        }

        public void StartCapturing()
        {
            const string LOG_IDENT = "ClipsManager::StartCapturing";

            if (_isCapturing || _screenCapture is null || _replayBuffer is null)
                return;

            _isCapturing = true;
            _cancellationTokenSource = new CancellationTokenSource();

            int intervalMs = 1000 / CaptureFps;

            _captureTimer = new System.Threading.Timer(
                CaptureFrameCallback,
                null,
                0,
                intervalMs);

            if (_hotkeyManager is not null && ClipHotkey != 0)
            {
                var key = KeyInterop.KeyFromVirtualKey(ClipHotkey);
                _hotkeyManager.Register(key, 0, async () => await SaveClip());
            }

            SetStatus(ClipsStatus.Buffering);
            App.Logger.WriteLine(LOG_IDENT, "Started capturing");
        }

        public void StopCapturing()
        {
            const string LOG_IDENT = "ClipsManager::StopCapturing";

            _captureTimer?.Dispose();
            _captureTimer = null;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isCapturing = false;
            _replayBuffer?.Clear();

            if (_hotkeyManager is not null)
                _hotkeyManager.Unregister();

            SetStatus(ClipsStatus.Idle);
            App.Logger.WriteLine(LOG_IDENT, "Stopped capturing");
        }

        public void UpdateHotkey(int virtualKey)
        {
            ClipHotkey = virtualKey;

            if (_hotkeyManager is null)
                return;

            _hotkeyManager.Unregister();

            if (virtualKey != 0 && _isCapturing)
            {
                var key = KeyInterop.KeyFromVirtualKey(virtualKey);
                _hotkeyManager.Register(key, 0, async () => await SaveClip());
            }
        }

        private void CaptureFrameCallback(object? state)
        {
            if (_disposed || _isSaving || _screenCapture is null || _replayBuffer is null)
                return;

            if (_cancellationTokenSource?.IsCancellationRequested == true)
                return;

            if (!_screenCapture.IsTargetAvailable)
            {
                SetStatus(ClipsStatus.Idle);
                return;
            }

            try
            {
                using var frame = _screenCapture.CaptureFrame();
                if (frame is not null)
                    _replayBuffer.AddFrame(frame);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ClipsManager::CaptureFrameCallback", ex);
            }
        }

        public async Task SaveClip()
        {
            const string LOG_IDENT = "ClipsManager::SaveClip";

            if (_disposed || _isSaving || _replayBuffer is null)
                return;

            _isSaving = true;
            SetStatus(ClipsStatus.Saving);

            try
            {
                var frames = _replayBuffer.GetFramesSnapshot();

                if (frames.Count == 0)
                {
                    LastError = "No frames in buffer";
                    SetStatus(ClipsStatus.Error);
                    ErrorOccurred?.Invoke(this, LastError);
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"clip_{timestamp}.avi";
                string outputPath = Path.Combine(OutputFolder, filename);

                await Task.Run(() => AviWriter.WriteFramesToAvi(outputPath, frames, CaptureFps));

                LastError = "";
                SetStatus(ClipsStatus.Saved);
                ClipSaved?.Invoke(this, outputPath);

                App.Logger.WriteLine(LOG_IDENT, $"Clip saved: {outputPath}");

                await Task.Delay(2000);
                SetStatus(_isCapturing ? ClipsStatus.Buffering : ClipsStatus.Idle);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SetStatus(ClipsStatus.Error);
                ErrorOccurred?.Invoke(this, LastError);
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                _isSaving = false;
            }
        }

        public void OpenClipsFolder()
        {
            if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
                Process.Start("explorer.exe", OutputFolder);
        }

        private void SetStatus(ClipsStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopCapturing();
            _screenCapture?.Dispose();
            _replayBuffer?.Dispose();
            _hotkeyManager?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
