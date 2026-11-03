using System;
using System.Runtime.InteropServices;
using System.Windows;
using FlowCommand.Core.Util;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Detects a double-tap of ONE configured key (default: the backtick key) using a Windows low-level
    /// keyboard listener (WH_KEYBOARD_LL). For each key event it asks only "is this the trigger key, or some other
    /// key?" (other keys just cancel a half-finished double tap). It never records, stores, logs or sends what
    /// you type, ignores injected keys (such as Wispr's own paste), and blocks nothing: every key still reaches
    /// the application you're in, so a double-tapped backtick still types two backticks there.
    /// Presses made while Ctrl, Alt, Shift or Win is held never count (so Ctrl+` in VS Code is safe).
    /// </summary>
    internal sealed class DoubleTapKeyListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        private const int LLKHF_EXTENDED = 0x01, LLKHF_INJECTED = 0x10;
        private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        private readonly NativeMethods.LowLevelKeyboardProc _proc;   // field keeps the delegate alive for the hook's lifetime
        private DoubleTapDetector _detector = new DoubleTapDetector(400);
        private TriggerKey _key = TriggerKeys.Default;
        private IntPtr _hook = IntPtr.Zero;

        public event Action? DoubleTapped;

        public DoubleTapKeyListener() { _proc = HookProc; }

        public bool IsRunning => _hook != IntPtr.Zero;

        public bool Start(TriggerKey key, int intervalMs)
        {
            _key = key;
            _detector = new DoubleTapDetector(intervalMs);
            if (_hook != IntPtr.Zero) return true;
            _hook = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
            return _hook != IntPtr.Zero;
        }

        public void Stop()
        {
            if (_hook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        private static bool ModifierDown()
        {
            foreach (var vk in new[] { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN })
                if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            return false;
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    // KBDLLHOOKSTRUCT: vkCode @0, scanCode @4, flags @8
                    var msg = wParam.ToInt32();
                    var vk = Marshal.ReadInt32(lParam);
                    var scan = Marshal.ReadInt32(lParam, 4);
                    var flags = Marshal.ReadInt32(lParam, 8);
                    var injected = (flags & LLKHF_INJECTED) != 0;
                    var extended = (flags & LLKHF_EXTENDED) != 0;
                    var down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    var up = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                    if (!injected)
                    {
                        if (_key.Matches(vk, scan, extended))
                        {
                            if (down)
                            {
                                if (ModifierDown()) _detector.OtherKey();
                                else if (_detector.KeyDown(Environment.TickCount64))
                                {
                                    // Return from the hook immediately; do the real work on the UI thread.
                                    Application.Current?.Dispatcher.BeginInvoke(new Action(() => DoubleTapped?.Invoke()));
                                }
                            }
                            else if (up) _detector.KeyUp();
                        }
                        else if (down)
                        {
                            _detector.OtherKey();
                        }
                    }
                }
                catch { /* a hook must never throw */ }
            }
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose() => Stop();
    }
}
