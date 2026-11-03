using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;

namespace FlowCommand.Platform
{
    /// <summary>activate / minimize / maximize / restore a window using Win32 window APIs (no mouse automation).</summary>
    internal sealed class WindowActions : IActionHandler
    {
        private sealed record Target(List<IntPtr> Handles, string Label, string? Error);

        private readonly AppResolver _resolver;
        public WindowActions(AppResolver resolver) { _resolver = resolver; }

        public IReadOnlyCollection<string> Actions { get; } =
            new[] { "activate_window", "minimize_window", "maximize_window", "restore_window" };

        public async Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            var t = FindTarget(plan, ctx);
            if (t.Error != null) return ActionResult.Fail(t.Error);

            switch (plan.Action)
            {
                case "activate_window":
                {
                    var h = t.Handles[0];
                    WindowTools.ForceForeground(h);
                    await Task.Delay(250, ct);
                    return NativeMethods.GetForegroundWindow() == h
                        ? new ActionResult(true, $"Switched to {t.Label}.", true, false, true)
                        : new ActionResult(true, $"Asked Windows to bring {t.Label} forward, but it may not have taken focus.", false, false, true);
                }
                case "minimize_window":
                {
                    foreach (var h in t.Handles) NativeMethods.ShowWindow(h, NativeMethods.SW_MINIMIZE);
                    await Task.Delay(250, ct);
                    return t.Handles.All(NativeMethods.IsIconic)
                        ? new ActionResult(true, $"Minimised {t.Label}.")
                        : new ActionResult(true, $"Asked Windows to minimise {t.Label}, but it didn't confirm.", false);
                }
                case "maximize_window":
                {
                    foreach (var h in t.Handles)
                    {
                        if (NativeMethods.IsIconic(h)) NativeMethods.ShowWindow(h, NativeMethods.SW_RESTORE);
                        NativeMethods.ShowWindow(h, NativeMethods.SW_MAXIMIZE);
                    }
                    await Task.Delay(250, ct);
                    return t.Handles.All(NativeMethods.IsZoomed)
                        ? new ActionResult(true, $"Maximised {t.Label}.")
                        : new ActionResult(true, $"Asked Windows to maximise {t.Label}, but it didn't confirm (some windows can't be maximised).", false);
                }
                default: // restore_window
                {
                    foreach (var h in t.Handles) NativeMethods.ShowWindow(h, NativeMethods.SW_RESTORE);
                    await Task.Delay(250, ct);
                    return t.Handles.All(h => !NativeMethods.IsIconic(h) && !NativeMethods.IsZoomed(h))
                        ? new ActionResult(true, $"Restored {t.Label}.")
                        : new ActionResult(true, $"Asked Windows to restore {t.Label}, but it didn't confirm.", false);
                }
            }
        }

        private Target FindTarget(ValidatedPlan plan, ActionContext ctx)
        {
            var q = plan.ArgOrNull("application");
            if (q != null)
            {
                var r = _resolver.Resolve(q);
                if (!r.Success) return new Target(new List<IntPtr>(), q, r.Error);
                var app = r.App!;
                if (app.ProcessName == null)
                    return new Target(new List<IntPtr>(), app.DisplayName,
                        $"Can't tell which windows belong to {app.DisplayName}. Set \"processName\" for it in config.json.");
                var wins = WindowTools.WindowsOf(app);
                if (wins.Count == 0)
                    return new Target(new List<IntPtr>(), app.DisplayName,
                        $"{app.DisplayName} isn't running. Say \"open {app.DisplayName}\" to launch it.");
                return new Target(wins.Select(w => w.Handle).ToList(), app.DisplayName, null);
            }

            // No application named: act on the window that had focus when the palette was opened.
            var h = ctx.PreviousForegroundWindow;
            if (h == IntPtr.Zero || !NativeMethods.IsWindow(h))
                return new Target(new List<IntPtr>(), "window", "There is no window to act on. Name the application.");
            NativeMethods.GetWindowThreadProcessId(h, out var pid);
            if (pid == (uint)Environment.ProcessId)
                return new Target(new List<IntPtr>(), "window", "There is no window to act on. Name the application.");
            var cls = WindowTools.ClassName(h);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd")
                return new Target(new List<IntPtr>(), "window", "The desktop or taskbar was in focus, so there's no window to act on. Name the application.");
            var title = WindowTools.TopLevelWindows().FirstOrDefault(w => w.Handle == h)?.Title ?? "the current window";
            return new Target(new List<IntPtr> { h }, title, null);
        }
    }
}
