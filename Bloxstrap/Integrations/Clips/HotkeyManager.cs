using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Bloxstrap.Integrations.Clips
{
    public class HotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9001;

        private IntPtr _windowHandle;
        private IntPtr _hookId;
        private bool _isRegistered;
        private bool _disposed;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private LowLevelKeyboardProc? _proc;
        private Key? _registeredKey;
        private int _registeredModifiers;
        private Action? _callback;

        public event EventHandler<Key>? HotkeyPressed;

        public HotkeyManager(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public bool Register(Key key, int modifiers, Action callback)
        {
            if (_isRegistered)
                Unregister();

            _registeredKey = key;
            _registeredModifiers = modifiers;
            _callback = callback;

            _proc = HookCallback;
            _hookId = SetHook(_proc);
            _isRegistered = true;

            App.Logger.WriteLine("HotkeyManager::Register", $"Registered hotkey {key} with modifiers {modifiers}");
            return true;
        }

        public void Unregister()
        {
            if (!_isRegistered)
                return;

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _isRegistered = false;
            _registeredKey = null;
            _callback = null;

            App.Logger.WriteLine("HotkeyManager::Unregister", "Unregistered hotkey");
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var module = process.MainModule;

            if (module?.ModuleName is null)
                return IntPtr.Zero;

            return SetWindowsHookEx(13, proc, GetModuleHandle(module.ModuleName), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_HOTKEY)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int modifiers = (int)lParam >> 16;

                if (_registeredKey.HasValue && _registeredModifiers == modifiers)
                {
                    int registeredVk = KeyInterop.VirtualKeyFromKey(_registeredKey.Value);

                    if (vkCode == registeredVk)
                    {
                        _callback?.Invoke();
                        HotkeyPressed?.Invoke(this, _registeredKey.Value);
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Unregister();
            GC.SuppressFinalize(this);
        }
    }
}
