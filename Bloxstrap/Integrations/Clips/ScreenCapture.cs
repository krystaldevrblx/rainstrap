using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Bloxstrap.Integrations.Clips
{
    public class ScreenCapture : IDisposable
    {
        private readonly IntPtr _targetWindowHandle;
        private bool _disposed;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public ScreenCapture(IntPtr targetWindowHandle)
        {
            _targetWindowHandle = targetWindowHandle;
        }

        public bool IsTargetAvailable
        {
            get
            {
                if (_targetWindowHandle == IntPtr.Zero)
                    return false;

                return IsWindow(_targetWindowHandle);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        public Bitmap? CaptureFrame()
        {
            if (!IsTargetAvailable)
                return null;

            try
            {
                if (!GetWindowRect(_targetWindowHandle, out RECT windowRect))
                    return null;

                int width = windowRect.Right - windowRect.Left;
                int height = windowRect.Bottom - windowRect.Top;

                if (width <= 0 || height <= 0)
                    return null;

                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(windowRect.Left, windowRect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
