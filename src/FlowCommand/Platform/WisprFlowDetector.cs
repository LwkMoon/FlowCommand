using System;
using System.Diagnostics;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Best-effort, read-only hint for the Welcome and Settings windows: is a program whose name contains "wispr" running?
    /// FlowCommand does not need Wispr Flow to work (anything that types text into the palette will do), does not talk to it,
    /// and never starts, stops or installs it. A "not running" result is information, not an error.
    /// </summary>
    internal static class WisprFlowDetector
    {
        public static bool IsRunning()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    using (p)
                    {
                        string name;
                        try { name = p.ProcessName; } catch { continue; }
                        if (name.IndexOf("wispr", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    }
                }
            }
            catch (Exception) { }
            return false;
        }
    }
}
