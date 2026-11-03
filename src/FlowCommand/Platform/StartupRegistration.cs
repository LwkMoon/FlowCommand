using System;
using Microsoft.Win32;

namespace FlowCommand.Platform
{
    /// <summary>Optional "start with Windows" via the per-user Run key (no admin needed).</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FlowCommand";

        public static bool IsEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k?.GetValue(ValueName) != null;
        }

        public static void Set(bool enabled)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    var exe = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exe)) k.SetValue(ValueName, "\"" + exe + "\"");
                }
                else k.DeleteValue(ValueName, false);
            }
        }
    }
}
