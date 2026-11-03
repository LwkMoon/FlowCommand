using System;
using System.Windows;
using System.Windows.Interop;
using FlowCommand.Core.Util;

namespace FlowCommand.Platform
{
    /// <summary>System-wide optional hotkey via RegisterHotKey on a hidden, never-shown window (no key listening). The double-tap trigger is a separate class.</summary>
    internal sealed class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 0x4F43;

        private readonly Window _window;
        private readonly HwndSource _source;
        private bool _registered;

        public event Action? Pressed;

        public HotkeyService()
        {
            _window = new Window
            {
                Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                ShowActivated = false, Visibility = Visibility.Hidden,
            };
            var handle = new WindowInteropHelper(_window).EnsureHandle();
            _source = HwndSource.FromHwnd(handle);
            _source.AddHook(Hook);
        }

        public bool Register(Hotkey hotkey)
        {
            Unregister();
            _registered = NativeMethods.RegisterHotKey(
                _source.Handle, HotkeyId, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, hotkey.VirtualKey);
            return _registered;
        }

        public void Unregister()
        {
            if (_registered) NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                Pressed?.Invoke();
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source.RemoveHook(Hook);
            _window.Close();
        }
    }
}
