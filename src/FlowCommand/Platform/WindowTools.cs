using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FlowCommand.Platform
{
    internal sealed record WindowInfo(IntPtr Handle, uint ProcessId, string Title);

    internal static class WindowTools
    {
        /// <summary>Visible, un-owned, titled top-level windows (what a person would call "a window"), in z-order.</summary>
        public static List<WindowInfo> TopLevelWindows()
        {
            var list = new List<WindowInfo>();
            NativeMethods.EnumWindows((h, _) =>
            {
                if (!NativeMethods.IsWindowVisible(h)) return true;
                if (NativeMethods.GetWindow(h, NativeMethods.GW_OWNER) != IntPtr.Zero) return true;
                if ((NativeMethods.GetWindowLong(h, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;
                if (IsCloaked(h)) return true;
                var len = NativeMethods.GetWindowTextLength(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(h, sb, sb.Capacity);
                NativeMethods.GetWindowThreadProcessId(h, out var pid);
                list.Add(new WindowInfo(h, pid, sb.ToString()));
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static bool IsCloaked(IntPtr h)
        {
            try { return NativeMethods.DwmGetWindowAttribute(h, NativeMethods.DWMWA_CLOAKED, out var v, sizeof(int)) == 0 && v != 0; }
            catch { return false; }
        }

        public static string ClassName(IntPtr h)
        {
            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static bool Matches(uint pid, ResolvedApp app)
        {
            if (app.ProcessName == null) return false;
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                {
                    if (!string.Equals(p.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase)) return false;
                    if (app.PathContains == null) return true;
                    var path = p.MainModule?.FileName ?? "";
                    return path.IndexOf(app.PathContains, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        public static List<WindowInfo> WindowsOf(ResolvedApp app) =>
            TopLevelWindows().Where(w => Matches(w.ProcessId, app)).ToList();

        /// <summary>Brings a window to the foreground despite Windows' foreground-lock rules.</summary>
        public static void ForceForeground(IntPtr hwnd)
        {
            if (NativeMethods.IsIconic(hwnd)) NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
            var fg = NativeMethods.GetForegroundWindow();
            uint fgThread = 0;
            if (fg != IntPtr.Zero) fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
            var cur = NativeMethods.GetCurrentThreadId();
            var attached = false;
            if (fgThread != 0 && fgThread != cur) attached = NativeMethods.AttachThreadInput(cur, fgThread, true);
            try
            {
                NativeMethods.BringWindowToTop(hwnd);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) NativeMethods.AttachThreadInput(cur, fgThread, false);
            }
        }
    }
}
