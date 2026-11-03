using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Safety;

namespace FlowCommand.Platform
{
    /// <summary>
    /// inspect_port (read-only) and kill_process_on_port (high risk, always confirmed).
    /// Listeners are found with the fixed command "netstat -ano" (language-independent parsing).
    /// </summary>
    internal sealed class PortActions : IActionHandler, IConfirmationPreview
    {
        private static readonly string[] ProtectedNames =
        {
            "system", "idle", "svchost", "csrss", "wininit", "services", "lsass", "winlogon", "smss",
            "explorer", "dwm", "fontdrvhost", "wispr", "flowcommand", "registry",
        };

        public IReadOnlyCollection<string> Actions { get; } = new[] { "inspect_port", "kill_process_on_port" };

        public async Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            var port = plan.IntArg("port");
            var pids = await FindListenerPidsAsync(port, ct);
            if (pids.Count == 0) return new ActionResult(true, $"Nothing is listening on TCP port {port}.");

            if (plan.Action == "inspect_port")
                return new ActionResult(true, $"Port {port} is in use by {string.Join(", ", pids.Select(Describe))}.");

            // kill_process_on_port
            foreach (var pid in pids)
                if (IsProtected(pid, out var why))
                    return ActionResult.Fail($"Refusing to terminate {Describe(pid)}: {why}.");

            var killed = new List<string>();
            foreach (var pid in pids)
            {
                var label = Describe(pid);
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        p.Kill(false);
                        p.WaitForExit(3000);
                    }
                    killed.Add(label);
                }
                catch (Exception ex)
                {
                    return ActionResult.Fail($"Couldn't terminate {label}: {ex.Message}");
                }
            }

            await Task.Delay(500, ct);
            var remaining = await FindListenerPidsAsync(port, ct);
            return remaining.Count == 0
                ? new ActionResult(true, $"Terminated {string.Join(", ", killed)}; port {port} is now free.")
                : new ActionResult(true, $"Terminated {string.Join(", ", killed)}, but port {port} is still in use by {string.Join(", ", remaining.Select(Describe))}.", false);
        }

        public async Task<string?> PreviewAsync(ValidatedPlan plan, CancellationToken ct)
        {
            if (plan.Action != "kill_process_on_port") return null;
            var port = plan.IntArg("port");
            var pids = await FindListenerPidsAsync(port, ct);
            return pids.Count == 0
                ? $"Nothing is listening on port {port} right now."
                : $"Will force-terminate {string.Join(", ", pids.Select(Describe))} (port {port}). Unsaved work in it is lost.";
        }

        private static async Task<List<int>> FindListenerPidsAsync(int port, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "netstat.exe"))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-ano");

            using (var p = Process.Start(psi)!)
            {
                var output = await p.StandardOutput.ReadToEndAsync(ct);
                await p.WaitForExitAsync(ct);

                var pids = new HashSet<int>();
                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5 || !parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase)) continue;
                    // Listening sockets have a foreign address ending in ":0" (0.0.0.0:0 or [::]:0).
                    if (!parts[1].EndsWith(":" + port, StringComparison.Ordinal) || !parts[2].EndsWith(":0", StringComparison.Ordinal)) continue;
                    if (int.TryParse(parts[parts.Length - 1], out var pid)) pids.Add(pid);
                }
                return pids.ToList();
            }
        }

        private static string Describe(int pid)
        {
            try { using (var p = Process.GetProcessById(pid)) return $"{p.ProcessName} (PID {pid})"; }
            catch { return $"unknown process (PID {pid})"; }
        }

        private static bool IsProtected(int pid, out string why)
        {
            why = "";
            if (pid <= 4) { why = "it is a core system process"; return true; }
            if (pid == Environment.ProcessId) { why = "that is FlowCommand itself"; return true; }
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    var name = p.ProcessName.ToLowerInvariant();
                    if (ProtectedNames.Any(n => name.Contains(n)) || ProtectedProcesses.IsProtected(p.ProcessName))
                    {
                        why = "it is a protected system or voice-input process";
                        return true;
                    }
                }
            }
            catch { why = "it can't be inspected"; return true; }
            return false;
        }
    }
}
