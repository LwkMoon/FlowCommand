using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Safety;

namespace FlowCommand.Platform
{
    /// <summary>open_application / close_application. Closing sends WM_CLOSE only: never kills processes.</summary>
    internal sealed class ApplicationActions : IActionHandler
    {
        private readonly AppResolver _resolver;
        public ApplicationActions(AppResolver resolver) { _resolver = resolver; }

        public IReadOnlyCollection<string> Actions { get; } = new[] { "open_application", "close_application" };

        public Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct) =>
            plan.Action == "open_application" ? OpenAsync(plan, ct) : CloseAsync(plan, ct);

        private async Task<ActionResult> OpenAsync(ValidatedPlan plan, CancellationToken ct)
        {
            var r = _resolver.Resolve(plan.Arg("application"));
            if (!r.Success) return ActionResult.Fail(r.Error!);
            var app = r.App!;
            try
            {
                Process.Start(new ProcessStartInfo(app.LaunchTarget) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                return ActionResult.Fail($"Couldn't launch {app.DisplayName}: {ex.Message}");
            }

            if (app.ProcessName == null)
                return new ActionResult(true, $"Asked Windows to launch {app.DisplayName}. A window couldn't be verified for this app.", false);

            for (var i = 0; i < 14; i++)
            {
                if (WindowTools.WindowsOf(app).Count > 0)
                    return new ActionResult(true, $"Opened {app.DisplayName}.");
                await Task.Delay(250, ct);
            }
            return new ActionResult(true, $"Launched {app.DisplayName}, but no window has appeared yet.", false);
        }

        private async Task<ActionResult> CloseAsync(ValidatedPlan plan, CancellationToken ct)
        {
            var r = _resolver.Resolve(plan.Arg("application"));
            if (!r.Success) return ActionResult.Fail(r.Error!);
            var app = r.App!;
            if (app.ProcessName == null)
                return ActionResult.Fail($"FlowCommand can't tell which process belongs to {app.DisplayName}, so it won't close it. Set \"processName\" for it in config.json.");

            if (ProtectedProcesses.IsProtected(app.ProcessName))
                return ActionResult.Fail($"{app.DisplayName} belongs to a protected system or voice-input process, so FlowCommand won't close it.");

            var windows = WindowTools.WindowsOf(app);
            if (windows.Count == 0)
                return new ActionResult(true, $"{app.DisplayName} has no open windows. Nothing to close.");

            foreach (var w in windows) NativeMethods.PostMessage(w.Handle, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            for (var i = 0; i < 12; i++)
            {
                await Task.Delay(250, ct);
                if (WindowTools.WindowsOf(app).Count == 0)
                    return new ActionResult(true, $"Closed {app.DisplayName} ({windows.Count} window{(windows.Count == 1 ? "" : "s")}).");
            }
            var left = WindowTools.WindowsOf(app).Count;
            return new ActionResult(true,
                $"Asked {app.DisplayName} to close, but {left} window{(left == 1 ? " is" : "s are")} still open. It may be waiting for you to save.", false);
        }
    }
}
