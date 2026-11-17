using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace FlowCommand.Platform
{
    /// <summary>
    /// Optional "start when I sign in". Off by default and only changed from the Settings or Welcome window.
    /// Store (MSIX) install: the supported Windows startup task, which the user can also manage under
    /// Settings, Apps, Startup. Zip install: the per-user Run key (no admin needed).
    /// </summary>
    internal static class StartupRegistration
    {
        /// <summary>Must match the TaskId in packaging\AppxManifest.xml.</summary>
        public const string TaskId = "FlowCommandStartup";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "FlowCommand";

        public static async Task<bool> IsEnabledAsync()
        {
            if (!PackageIdentity.IsPackaged)
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k?.GetValue(ValueName) != null;
            }
            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                return task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Returns null on success, otherwise a message to show the user.</summary>
        public static async Task<string?> SetAsync(bool enabled)
        {
            if (!PackageIdentity.IsPackaged)
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
                return null;
            }

            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (!enabled)
                {
                    if (task.State == StartupTaskState.Enabled) task.Disable();
                    return null;
                }
                if (task.State == StartupTaskState.Enabled || task.State == StartupTaskState.EnabledByPolicy) return null;

                var state = await task.RequestEnableAsync();
                return state == StartupTaskState.Enabled
                    ? null
                    : "Windows is not allowing FlowCommand to start with Windows. You can turn it on under Settings, Apps, Startup.";
            }
            catch (Exception ex)
            {
                return "Couldn't change the startup setting: " + ex.Message;
            }
        }
    }
}
