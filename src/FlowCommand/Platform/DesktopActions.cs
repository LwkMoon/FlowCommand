using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;

namespace FlowCommand.Platform
{
    /// <summary>show_desktop / restore_windows via the documented Shell.Application MinimizeAll / UndoMinimizeALL methods.</summary>
    internal sealed class DesktopActions : IActionHandler
    {
        public IReadOnlyCollection<string> Actions { get; } = new[] { "show_desktop", "restore_windows" };

        public async Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            var minimize = plan.Action == "show_desktop";
            try { ShellCall(minimize); }
            catch (Exception ex) { return ActionResult.Fail($"Windows didn't accept the request: {ex.Message}"); }

            await Task.Delay(600, ct);
            var own = (uint)Environment.ProcessId;
            var visible = WindowTools.TopLevelWindows()
                .Where(w => w.ProcessId != own && !NativeMethods.IsIconic(w.Handle))
                .Count();

            if (minimize)
                return visible == 0
                    ? new ActionResult(true, "Showing the desktop.")
                    : new ActionResult(true, $"Asked Windows to show the desktop; {visible} window{(visible == 1 ? " is" : "s are")} still visible (always-on-top or non-minimisable).", false);
            return new ActionResult(true, "Asked Windows to restore the minimised windows.", false);
        }

        private static void ShellCall(bool minimizeAll)
        {
            var t = Type.GetTypeFromProgID("Shell.Application")
                    ?? throw new InvalidOperationException("Shell.Application is not available.");
            dynamic shell = Activator.CreateInstance(t)!;
            try
            {
                if (minimizeAll) shell.MinimizeAll(); else shell.UndoMinimizeALL();
            }
            finally { Marshal.FinalReleaseComObject((object)shell); }
        }
    }
}
