namespace FlowCommand.Core.Actions;

public enum RiskLevel { Low = 0, Moderate = 1, High = 2 }

/// <summary>Name = friendly identifier (letters/digits/space/.-_+#'()), Text = free text, Integer, Enum.</summary>
public enum ParamKind { Name, Text, Integer, Enum }

public sealed record ParamSpec(
    string Name, ParamKind Kind, bool Required, string Description,
    IReadOnlyList<string>? AllowedValues = null, int Min = 0, int Max = 0);

public sealed record ActionSpec(
    string Name, string Description, RiskLevel Risk,
    IReadOnlyList<ParamSpec> Parameters, bool Meta = false);

/// <summary>
/// The ONLY operations this program can ever perform. The AI chooses from this list;
/// the local policy decides whether they are allowed. Adding a capability = add a spec here
/// plus an IActionHandler in the app project.
/// </summary>
public static class ActionCatalog
{
    public const string Clarify = "clarify";
    public const string Unsupported = "unsupported";

    /// <summary>Settings page name -> ms-settings URI. These are the only pages open_settings can reach.</summary>
    public static readonly IReadOnlyDictionary<string, string> SettingsPageUris = new Dictionary<string, string>
    {
        ["bluetooth"] = "ms-settings:bluetooth",
        ["wifi"] = "ms-settings:network-wifi",
        ["network"] = "ms-settings:network-status",
        ["vpn"] = "ms-settings:network-vpn",
        ["airplane_mode"] = "ms-settings:network-airplanemode",
        ["sound"] = "ms-settings:sound",
        ["display"] = "ms-settings:display",
        ["night_light"] = "ms-settings:nightlight",
        ["notifications"] = "ms-settings:notifications",
        ["power"] = "ms-settings:powersleep",
        ["battery"] = "ms-settings:batterysaver",
        ["storage"] = "ms-settings:storagesense",
        ["apps"] = "ms-settings:appsfeatures",
        ["default_apps"] = "ms-settings:defaultapps",
        ["startup_apps"] = "ms-settings:startupapps",
        ["mouse"] = "ms-settings:mousetouchpad",
        ["typing"] = "ms-settings:typing",
        ["printers"] = "ms-settings:printers",
        ["privacy"] = "ms-settings:privacy",
        ["microphone_privacy"] = "ms-settings:privacy-microphone",
        ["camera_privacy"] = "ms-settings:privacy-webcam",
        ["accounts"] = "ms-settings:yourinfo",
        ["date_time"] = "ms-settings:dateandtime",
        ["language"] = "ms-settings:regionlanguage",
        ["windows_update"] = "ms-settings:windowsupdate",
        ["personalization"] = "ms-settings:personalization",
        ["background"] = "ms-settings:personalization-background",
        ["colors"] = "ms-settings:colors",
        ["lock_screen"] = "ms-settings:lockscreen",
        ["taskbar"] = "ms-settings:taskbar",
        ["themes"] = "ms-settings:themes",
        ["about"] = "ms-settings:about",
        ["troubleshoot"] = "ms-settings:troubleshoot",
        ["accessibility"] = "ms-settings:easeofaccess",
        ["clipboard"] = "ms-settings:clipboard",
        ["multitasking"] = "ms-settings:multitasking",
    };

    public static readonly IReadOnlyList<string> SettingsPages = SettingsPageUris.Keys.ToList();

    public static readonly IReadOnlyList<string> BuiltInFolders = new[]
    {
        "downloads", "documents", "desktop", "pictures", "music", "videos", "home",
        "screenshots", "onedrive", "appdata", "temp", "startup", "recycle_bin", "this_pc",
    };

    public static readonly IReadOnlyList<string> MediaCommands = new[]
    {
        "play_pause", "next_track", "previous_track", "stop", "volume_up", "volume_down", "mute",
    };

    /// <summary>Actions introduced with config version 2; enabled automatically for configs written by older releases.</summary>
    public static readonly IReadOnlyList<string> AddedInConfigVersion2 = new[]
    {
        "media_control", "lock_workstation", "take_screenshot", "open_website", "search_web",
    };

    private static ParamSpec N(string name, string desc, bool required = true) =>
        new(name, ParamKind.Name, required, desc);

    public static readonly IReadOnlyList<ActionSpec> All = new List<ActionSpec>
    {
        new("open_application", "Launch an installed application.", RiskLevel.Low,
            new[] { N("application", "app id from the configured list, or the plain app name") }),
        new("close_application", "Gracefully close all windows of an application (WM_CLOSE, so it can prompt to save).", RiskLevel.Moderate,
            new[] { N("application", "app id or plain app name") }),
        new("activate_window", "Bring a running application's window to the front (restores it if minimised).", RiskLevel.Low,
            new[] { N("application", "app id or plain app name") }),
        new("minimize_window", "Minimise an application's windows, or the window the user was just using if no application is given.", RiskLevel.Low,
            new[] { N("application", "app id or plain app name; omit for the current window", false) }),
        new("maximize_window", "Maximise an application's window, or the window the user was just using if no application is given.", RiskLevel.Low,
            new[] { N("application", "app id or plain app name; omit for the current window", false) }),
        new("restore_window", "Restore a minimised/maximised window to its normal size.", RiskLevel.Low,
            new[] { N("application", "app id or plain app name; omit for the current window", false) }),
        new("show_desktop", "Minimise all windows to show the desktop.", RiskLevel.Low,
            Array.Empty<ParamSpec>()),
        new("restore_windows", "Undo 'show desktop' and bring minimised windows back.", RiskLevel.Low,
            Array.Empty<ParamSpec>()),
        new("open_folder", "Open a folder in File Explorer.", RiskLevel.Low,
            new[] { N("folder", "a built-in folder (" + string.Join(", ", BuiltInFolders) + ") or a configured folder name") }),
        new("open_project", "Open a configured project folder in the user's code editor.", RiskLevel.Low,
            new[] { N("project", "project name as listed in the configured projects") }),
        new("open_settings", "Open a Windows Settings page.", RiskLevel.Low,
            new[] { new ParamSpec("page", ParamKind.Enum, true, "settings page", SettingsPages) }),
        new("bluetooth_set", "Turn Bluetooth explicitly on or off (never a blind toggle; the result is verified).", RiskLevel.Moderate,
            new[] { new ParamSpec("state", ParamKind.Enum, true, "desired state", new[] { "on", "off" }) }),
        new("inspect_port", "Report which process is listening on a TCP port (read-only).", RiskLevel.Moderate,
            new[] { new ParamSpec("port", ParamKind.Integer, true, "TCP port number", null, 1, 65535) }),
        new("kill_process_on_port", "Forcefully terminate the process listening on a TCP port. Destructive: unsaved work in it is lost.", RiskLevel.High,
            new[] { new ParamSpec("port", ParamKind.Integer, true, "TCP port number", null, 1, 65535) }),

        new("media_control", "Press a media key: play_pause, next_track, previous_track, stop, volume_up, volume_down or mute. For volume, 'steps' repeats the key (each step is about 2%; default 5).", RiskLevel.Low,
            new[]
            {
                new ParamSpec("command", ParamKind.Enum, true, "media key", MediaCommands),
                new ParamSpec("steps", ParamKind.Integer, false, "repeat count for the volume keys", null, 1, 25),
            }),
        new("lock_workstation", "Lock the Windows session. The password, PIN or Windows Hello is needed to return.", RiskLevel.Moderate,
            Array.Empty<ParamSpec>()),
        new("take_screenshot", "Take a screenshot: 'full' saves the whole screen to Pictures\\Screenshots, 'region' opens the snipping selector.", RiskLevel.Low,
            new[] { new ParamSpec("mode", ParamKind.Enum, true, "screenshot mode", new[] { "full", "region" }) }),
        new("open_website", "Open a website from the configured websites list in the browser.", RiskLevel.Low,
            new[] { N("site", "a website name from the configured list") }),
        new("search_web", "Search the web for the given words in the browser.", RiskLevel.Low,
            new[] { new ParamSpec("query", ParamKind.Text, true, "the words to search for") }),

        // Meta actions never touch the PC.
        new(Clarify, "Ask ONE short question when the request is ambiguous. Nothing is executed.", RiskLevel.Low,
            new[] { new ParamSpec("question", ParamKind.Text, true, "the question to show the user") }, Meta: true),
        new(Unsupported, "The request needs something not listed here. Nothing is executed.", RiskLevel.Low,
            new[] { new ParamSpec("reason", ParamKind.Text, true, "short reason shown to the user") }, Meta: true),
    }.AsReadOnly();

    public static ActionSpec? Find(string? name) =>
        name == null ? null : All.FirstOrDefault(a => a.Name == name);

    public static IReadOnlyList<string> DefaultEnabled() =>
        All.Where(a => !a.Meta).Select(a => a.Name).ToList();
}
