using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;

namespace FlowCommand.Platform
{
    /// <summary>
    /// media_control, lock_workstation and take_screenshot. Media control sends the same keys a keyboard's media
    /// buttons send; Windows does not report back whether a key had an effect, so those results are marked unverified.
    /// </summary>
    internal sealed class SystemActions : IActionHandler
    {
        private const byte VK_LWIN = 0x5B, VK_SNAPSHOT = 0x2C;

        private static readonly Dictionary<string, (byte Vk, string Label)> MediaKeys = new Dictionary<string, (byte, string)>
        {
            ["play_pause"] = (0xB3, "play/pause"),
            ["next_track"] = (0xB0, "next track"),
            ["previous_track"] = (0xB1, "previous track"),
            ["stop"] = (0xB2, "stop"),
            ["volume_up"] = (0xAF, "volume up"),
            ["volume_down"] = (0xAE, "volume down"),
            ["mute"] = (0xAD, "mute"),
        };

        public IReadOnlyCollection<string> Actions { get; } = new[] { "media_control", "lock_workstation", "take_screenshot" };

        public Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            switch (plan.Action)
            {
                case "media_control": return MediaAsync(plan, ct);
                case "lock_workstation": return Task.FromResult(Lock());
                default: return ScreenshotAsync(plan, ctx, ct);
            }
        }

        // ---------- media keys ----------
        private static async Task<ActionResult> MediaAsync(ValidatedPlan plan, CancellationToken ct)
        {
            var command = plan.Arg("command");
            if (!MediaKeys.TryGetValue(command, out var key)) return ActionResult.Fail($"Unknown media command: {command}");

            var isVolume = command.StartsWith("volume", StringComparison.Ordinal);
            var steps = 1;
            if (isVolume) steps = plan.Args.ContainsKey("steps") ? Math.Clamp(plan.IntArg("steps"), 1, 25) : 5;

            for (var i = 0; i < steps; i++)
            {
                PressKey(key.Vk);
                if (i < steps - 1) await Task.Delay(25, ct);
            }
            var times = steps > 1 ? $" \u00D7{steps}" : "";
            return new ActionResult(true, $"Sent {key.Label}{times}. Windows doesn't report whether it took effect.", false);
        }

        private static void PressKey(byte vk)
        {
            NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            NativeMethods.keybd_event(vk, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ---------- lock ----------
        private static ActionResult Lock() =>
            NativeMethods.LockWorkStation()
                ? new ActionResult(true, "Locking the PC.")
                : ActionResult.Fail("Windows refused to lock the session.");

        // ---------- screenshots ----------
        private static async Task<ActionResult> ScreenshotAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            var region = plan.Arg("mode") == "region";
            ctx.HideUi?.Invoke();                 // keep the palette out of the picture
            await Task.Delay(400, ct);

            if (region)
            {
                try { Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
                catch (Exception ex) { return ActionResult.Fail($"Couldn't open the screenshot selector: {ex.Message}"); }
                return new ActionResult(true, "Opened the screenshot selector. Drag to choose an area.", false, false, true);
            }

            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
            var since = DateTime.Now.AddSeconds(-2);
            SendWinPrintScreen();
            for (var i = 0; i < 16; i++)
            {
                await Task.Delay(250, ct);
                var file = NewestSince(folder, since);
                if (file != null)
                    return new ActionResult(true, $"Saved a screenshot: {Path.GetFileName(file)} (Pictures\\Screenshots).", true, false, true);
            }
            return new ActionResult(true, "Sent Win+PrintScreen, but no new file appeared in Pictures\\Screenshots.", false, false, true);
        }

        private static void SendWinPrintScreen()
        {
            NativeMethods.keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_SNAPSHOT, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_SNAPSHOT, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_LWIN, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static string? NewestSince(string folder, DateTime since)
        {
            try
            {
                if (!Directory.Exists(folder)) return null;
                return Directory.EnumerateFiles(folder)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime >= since)
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => f.FullName)
                    .FirstOrDefault();
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
