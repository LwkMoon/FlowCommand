using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlowCommand.Core.Actions;
using Windows.Devices.Radios;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Explicit Bluetooth on/off using the WinRT Windows.Devices.Radios API (no admin needed).
    /// Reads the current state first, sets the requested state, then re-reads to verify.
    /// If anything is unavailable it says so and opens Windows' Bluetooth settings instead.
    /// </summary>
    internal sealed class BluetoothActions : IActionHandler
    {
        public IReadOnlyCollection<string> Actions { get; } = new[] { "bluetooth_set" };

        public async Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext ctx, CancellationToken ct)
        {
            var desired = plan.Arg("state") == "on" ? RadioState.On : RadioState.Off;
            var word = desired == RadioState.On ? "on" : "off";
            try
            {
                var access = await Radio.RequestAccessAsync();
                if (access != RadioAccessStatus.Allowed)
                    return Fallback($"Windows denied radio access ({access})");

                var radios = await GetBluetoothRadiosAsync();
                if (radios.Count == 0) return Fallback("No Bluetooth radio was found");
                if (radios.All(r => r.State == desired)) return new ActionResult(true, $"Bluetooth is already {word}.");
                if (radios.Any(r => r.State == RadioState.Disabled))
                    return Fallback("The Bluetooth radio is disabled in hardware or device settings");

                foreach (var radio in radios.Where(x => x.State != desired))
                {
                    var status = await radio.SetStateAsync(desired);
                    if (status != RadioAccessStatus.Allowed)
                        return Fallback($"Windows refused to change the radio ({status})");
                }

                for (var i = 0; i < 12; i++)
                {
                    await Task.Delay(250, ct);
                    var now = await GetBluetoothRadiosAsync();
                    if (now.Count > 0 && now.All(r => r.State == desired))
                        return new ActionResult(true, $"Bluetooth is now {word} (verified).");
                }
                return Fallback($"Windows was asked to turn Bluetooth {word}, but the new state couldn't be confirmed");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return Fallback($"The Bluetooth API reported an error ({ex.Message})");
            }
        }

        private static async Task<List<Radio>> GetBluetoothRadiosAsync()
        {
            var all = await Radio.GetRadiosAsync();
            return all.Where(r => r.Kind == RadioKind.Bluetooth).ToList();
        }

        private static ActionResult Fallback(string reason)
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch { }
            return new ActionResult(false,
                $"{reason}, so Bluetooth was NOT changed. Windows Bluetooth settings were opened so you can switch it manually.", false);
        }
    }
}
