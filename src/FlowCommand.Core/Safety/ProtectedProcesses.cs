namespace FlowCommand.Core.Safety;

/// <summary>
/// Processes FlowCommand will never close, whatever the configuration says: core Windows components and the
/// voice-input tool itself. Applies even to apps a user adds with a process name.
/// </summary>
public static class ProtectedProcesses
{
    private static readonly HashSet<string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "winlogon", "csrss", "wininit", "services", "lsass", "svchost", "smss", "system", "idle",
        "registry", "fontdrvhost", "sihost", "shellexperiencehost", "startmenuexperiencehost", "searchhost",
        "runtimebroker", "ctfmon", "audiodg", "taskhostw", "conhost",
    };

    public static bool IsProtected(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return Exact.Contains(name)
            || name.Contains("wispr", StringComparison.OrdinalIgnoreCase)
            || name.Contains("flowcommand", StringComparison.OrdinalIgnoreCase);
    }
}
