using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using FlowCommand.Core.Util;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Gathers facts about the control that currently has keyboard focus (classic text caret + UI Automation)
    /// and hands them to the pure, unit-tested <see cref="FocusClassifier"/>. Run it off the UI thread.
    /// </summary>
    internal static class TextFocusDetector
    {
        public static FocusDecision Detect(IReadOnlyCollection<string> webContentProcesses)
        {
            try
            {
                var fg = NativeMethods.GetForegroundWindow();
                if (fg == IntPtr.Zero) return new FocusDecision(FocusKind.Unknown, "no foreground window");

                var cls = WindowTools.ClassName(fg);
                var shell = cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";

                var threadId = NativeMethods.GetWindowThreadProcessId(fg, out _);
                var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
                var hasCaret = NativeMethods.GetGUIThreadInfo(threadId, ref info) && info.hwndCaret != IntPtr.Zero;

                if (shell || hasCaret)
                    return FocusClassifier.Classify(new FocusSignals("", false, false, false, false, hasCaret, shell));

                var el = AutomationElement.FocusedElement;
                if (el == null) return new FocusDecision(FocusKind.Unknown, "no focused element reported");

                var cur = el.Current;
                var typeName = cur.ControlType.ProgrammaticName;                 // e.g. "ControlType.Edit"
                var dot = typeName.LastIndexOf('.');
                if (dot >= 0) typeName = typeName.Substring(dot + 1);

                var hasValue = el.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj);
                var readOnly = hasValue && valueObj is ValuePattern vp && vp.Current.IsReadOnly;
                var hasText = el.TryGetCurrentPattern(TextPattern.Pattern, out _);

                return FocusClassifier.Classify(new FocusSignals(
                    typeName, hasValue, readOnly, hasText, IsWebContent(cur.ProcessId, webContentProcesses), false, false));
            }
            catch (Exception ex)
            {
                return new FocusDecision(FocusKind.Unknown, "couldn't inspect focus: " + ex.GetType().Name);
            }
        }

        private static bool IsWebContent(int pid, IReadOnlyCollection<string> names)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                    return names.Any(n => string.Equals(n, p.ProcessName, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }
    }
}
