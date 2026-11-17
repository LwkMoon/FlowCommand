using System.Text.Json;
using System.Text.RegularExpressions;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;

namespace FlowCommand.Core.Ai;

/// <summary>
/// Offline interpreter: turns common English commands into the same structured plan an AI provider would return,
/// using plain pattern matching against the user's own configuration (apps, folders, websites, projects).
/// No network, no key, nothing leaves the PC. Its output goes through the same strict validation and risk policy
/// as an online AI's, so it can never do more than the enabled actions allow.
/// </summary>
public sealed class LocalRuleProvider : IAiProvider
{
    public const string UnsupportedReason =
        "Offline mode did not recognise that. Try \"open Chrome\", \"open downloads\", \"volume up\" or \"search for cats\", " +
        "or add an AI key in Settings for free-form commands.";

    private readonly AppConfig _cfg;

    public LocalRuleProvider(AppConfig cfg) { _cfg = cfg; }

    public string Name => ProviderCatalog.Local;

    public Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.CommandText))
            throw new AiProviderException("Nothing to interpret.", AiFailure.BadResponse);
        return Task.FromResult(Interpret(request.CommandText));
    }

    // ------------------------------------------------------------------ text clean-up

    private static readonly Regex LeadingFiller = new(
        @"^(?:(?:um|uh|hey|ok|okay|so|hi|please|can you|could you|would you|will you|go ahead and|i want you to|i want to|id like you to|id like to|i would like to|lets|just|now)\s+)+",
        RegexOptions.Compiled);

    private static readonly Regex TrailingFiller = new(
        @"(?:\s+(?:please|for me|now|thanks|thank you))+$", RegexOptions.Compiled);

    /// <summary>Lower-cases, drops punctuation and filler words so "Okay, open Chrome, please." becomes "open chrome".</summary>
    public static string Clean(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
        {
            if (ch == '\'' || ch == '\u2019') continue;                 // what's -> whats
            sb.Append(char.IsLetterOrDigit(ch) || ch == '+' || ch == '#' ? ch : ' ');
        }
        var t = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        t = LeadingFiller.Replace(t, "");
        t = TrailingFiller.Replace(t, "");
        return t.Trim();
    }

    // ------------------------------------------------------------------ plan output

    private static string Plan(string action, Dictionary<string, object>? parameters, string explanation, bool confirm = false) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["action"] = action,
            ["parameters"] = parameters ?? new Dictionary<string, object>(),
            ["requires_confirmation"] = confirm,
            ["explanation"] = explanation,
        });

    private static string Clarify(string question) =>
        Plan(ActionCatalog.Clarify, new() { ["question"] = question }, "Asking a question.");

    private static string Unsupported(string reason = UnsupportedReason) =>
        Plan(ActionCatalog.Unsupported, new() { ["reason"] = reason }, "Not something offline mode can do.");

    private static bool Is(string t, string pattern, out Match m)
    {
        m = Regex.Match(t, pattern, RegexOptions.CultureInvariant);
        return m.Success;
    }

    // ------------------------------------------------------------------ the rules

    /// <summary>Public so tests and the settings "Test" button can call it without an AiRequest.</summary>
    public string Interpret(string rawText)
    {
        var t = Clean(rawText);
        if (t.Length == 0) return Unsupported();

        var startsWithOpen = Regex.IsMatch(t, @"^(?:open|launch|start|run|go to|goto|visit|load|pull up)\b");

        // ---- port inspection comes before "kill"/"close" so those words are not misread
        if (Is(t, @"^(?:whats|what is|who is|whos|check|inspect|find)\s+(?:using|on|listening on|running on|at)?\s*(?:tcp\s+)?port\s+(\d{1,5})$", out var pm))
            return PortPlan(pm.Groups[1].Value, kill: false);
        if (Is(t, @"^(?:kill|stop|terminate|end|free up|free)\s+(?:the\s+)?(?:process|thing|app|server|program|task)?\s*(?:on|using|listening on|at)?\s*(?:tcp\s+)?port\s+(\d{1,5})$", out pm))
            return PortPlan(pm.Groups[1].Value, kill: true);

        // ---- screenshot
        if (!startsWithOpen && Regex.IsMatch(t, @"\b(?:screenshot|screen shot|screen capture|snip|screen clip)\b"))
        {
            var region = Regex.IsMatch(t, @"\b(?:region|area|selection|select|snip|clip|part)\b");
            return Plan("take_screenshot", new() { ["mode"] = region ? "region" : "full" },
                region ? "Opening the snipping selector." : "Taking a screenshot.");
        }

        // ---- lock
        if (Is(t, @"^lock(?:\s+(?:my|the|this))?(?:\s+(?:pc|computer|laptop|screen|workstation|windows|session|it))?$", out _))
            return Plan("lock_workstation", null, "Locking the PC.");

        // ---- bluetooth on/off
        if (Is(t, @"^(?:turn|switch|set)\s+(on|off)\s+(?:the\s+|my\s+)?bluetooth$", out var bm)
            || Is(t, @"^(?:turn|switch|set)\s+(?:the\s+|my\s+)?bluetooth\s+(on|off)$", out bm)
            || Is(t, @"^bluetooth\s+(on|off)$", out bm))
            return BluetoothPlan(bm.Groups[1].Value);
        if (Is(t, @"^(enable|disable)\s+(?:the\s+|my\s+)?bluetooth$", out bm))
            return BluetoothPlan(bm.Groups[1].Value == "enable" ? "on" : "off");

        // ---- show / restore desktop and window controls
        if (Is(t, @"^(?:show|hide)(?:\s+the)?\s+desktop$", out _) || Is(t, @"^minimi[sz]e\s+(?:all|everything)(?:\s+windows)?$", out _))
            return Plan("show_desktop", null, "Showing the desktop.");
        if (Is(t, @"^(?:restore|bring back|undo show desktop)(?:\s+(?:all|my|the))?\s*windows$", out _) || t == "undo show desktop")
            return Plan("restore_windows", null, "Bringing the windows back.");

        if (Is(t, @"^(minimi[sz]e|maximi[sz]e|restore)(?:\s+(?:the\s+)?(.+?))?(?:\s+window)?$", out var wm))
        {
            var verb = wm.Groups[1].Value.StartsWith("min") ? "minimize" : wm.Groups[1].Value.StartsWith("max") ? "maximize" : "restore";
            var target = wm.Groups[2].Success ? wm.Groups[2].Value.Trim() : "";
            var current = target.Length == 0 || target is "this" or "it" or "window" or "current" or "current window" or "this window";
            var action = verb + "_window";
            var past = verb == "minimize" ? "Minimising" : verb == "maximize" ? "Maximising" : "Restoring";
            if (current) return Plan(action, null, $"{past} the current window.");
            var app = ResolveApp(target, out var question);
            if (question != null) return Clarify(question);
            return Plan(action, new() { ["application"] = app.Id }, $"{past} {app.Display}.");
        }

        // ---- media keys
        if (Is(t, @"^(?:play|pause|resume|unpause|play pause|play or pause|toggle(?: the)? (?:music|playback)|(?:pause|resume|play)(?: the)? (?:music|song|track|video|playback|it))$", out _))
            return Plan("media_control", new() { ["command"] = "play_pause" }, "Toggling play and pause.");
        if (Is(t, @"^(?:next|skip|skip it|next (?:track|song)|skip(?: this| the)? (?:track|song)|play(?: the)? next (?:track|song))$", out _))
            return Plan("media_control", new() { ["command"] = "next_track" }, "Skipping to the next track.");
        if (Is(t, @"^(?:previous|previous (?:track|song)|last (?:track|song)|go back(?: a (?:track|song))?|back a (?:track|song)|play(?: the)? previous (?:track|song))$", out _))
            return Plan("media_control", new() { ["command"] = "previous_track" }, "Going to the previous track.");
        if (Is(t, @"^stop(?: the)? (?:music|song|track|playback|playing)$", out _))
            return Plan("media_control", new() { ["command"] = "stop" }, "Stopping playback.");
        if (Is(t, @"^(?:mute|unmute)(?:\s+(?:the\s+)?(?:sound|volume|audio|pc|computer|speakers))?$", out _))
            return Plan("media_control", new() { ["command"] = "mute" }, "Toggling mute.");

        var vol = TryVolume(t);
        if (vol != null)
        {
            var p = new Dictionary<string, object> { ["command"] = vol.Value.Command };
            if (vol.Value.Steps != null) p["steps"] = vol.Value.Steps.Value;
            return Plan("media_control", p, vol.Value.Command == "volume_up" ? "Turning the volume up." : "Turning the volume down.");
        }

        // ---- search
        if (Is(t, @"^(?:search(?:\s+the web|\s+google|\s+online)?(?:\s+for)?|google|look up|lookup|web search(?:\s+for)?)\s*(.*)$", out var sm))
        {
            var q = sm.Groups[1].Value.Trim();
            if (q.Length == 0) return Clarify("What should I search for?");
            return Plan("search_web", new() { ["query"] = q }, $"Searching the web for {q}.");
        }

        // ---- close
        if (Is(t, @"^(?:close|quit|exit)\s*(?:the\s+|my\s+)?(.*?)(?:\s+(?:app|application|program|window|windows))?$", out var cm))
        {
            var target = cm.Groups[1].Value.Trim();
            if (target.Length == 0) return Clarify("Which app should I close?");
            var app = ResolveApp(target, out var question);
            if (question != null) return Clarify(question);
            return Plan("close_application", new() { ["application"] = app.Id }, $"Closing {app.Display}.");
        }

        // ---- switch to a running app
        if (Is(t, @"^(?:switch(?:\s+over)?\s+to|focus(?:\s+on)?|activate|go back to|bring up|alt tab to)\s+(?:the\s+|my\s+)?(.+?)(?:\s+(?:app|application|program|window))?$", out var am))
        {
            var app = ResolveApp(am.Groups[1].Value.Trim(), out var question);
            if (question != null) return Clarify(question);
            return Plan("activate_window", new() { ["application"] = app.Id }, $"Switching to {app.Display}.");
        }

        // ---- open <something>
        if (Is(t, @"^(?:open|launch|start|run|go to|goto|visit|load|pull up)\s*(.*)$", out var om))
            return OpenPlan(om.Groups[1].Value.Trim());

        // ---- settings without a verb ("bluetooth settings", "settings wifi")
        if (Is(t, @"^(.+?)\s+settings$", out var sp) && MatchSettingsPage(sp.Groups[1].Value) is { } page)
            return SettingsPlan(page);
        if (Is(t, @"^settings\s+(?:for\s+|to\s+)?(.+)$", out sp) && MatchSettingsPage(sp.Groups[1].Value) is { } page2)
            return SettingsPlan(page2);

        return Unsupported();
    }

    // ------------------------------------------------------------------ helpers per rule

    private string PortPlan(string digits, bool kill)
    {
        if (!int.TryParse(digits, out var port) || port is < 1 or > 65535)
            return Unsupported("That is not a valid port number (1 to 65535).");
        return kill
            ? Plan("kill_process_on_port", new() { ["port"] = port }, $"Ending the process listening on port {port}.", confirm: true)
            : Plan("inspect_port", new() { ["port"] = port }, $"Checking which process uses port {port}.");
    }

    private static string BluetoothPlan(string state) =>
        Plan("bluetooth_set", new() { ["state"] = state }, state == "on" ? "Turning Bluetooth on." : "Turning Bluetooth off.");

    private static readonly HashSet<string> VolumeFiller = new(StringComparer.Ordinal)
    {
        "turn", "up", "down", "it", "this", "the", "a", "bit", "little", "lot", "much", "way", "volume", "sound", "audio",
        "music", "louder", "quieter", "higher", "lower", "softer", "increase", "raise", "decrease", "reduce", "crank",
        "make", "bring", "slightly", "more", "less", "my", "pc", "computer", "speakers", "all", "max", "maximum", "by",
    };

    private static (string Command, int? Steps)? TryVolume(string t)
    {
        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Length > 8) return null;
        if (words.Any(w => !VolumeFiller.Contains(w))) return null;       // "turn up the brightness" must not become volume_up

        var up = words.Any(w => w is "up" or "louder" or "higher" or "increase" or "raise" or "crank");
        var down = words.Any(w => w is "down" or "quieter" or "lower" or "softer" or "decrease" or "reduce");
        if (up == down) return null;
        if (!words.Any(w => w is "volume" or "sound" or "audio" or "music" or "it" or "this" or "louder" or "quieter" or "turn" or "up" or "down")) return null;

        int? steps = null;
        if (Regex.IsMatch(t, @"\b(?:lot|much|way|max|maximum|all|crank)\b")) steps = 12;
        else if (Regex.IsMatch(t, @"\b(?:little|bit|slightly)\b")) steps = 2;
        return (up ? "volume_up" : "volume_down", steps);
    }

    private sealed record AppRef(string Id, string Display);

    /// <summary>Maps a spoken app name onto a configured app. Sets <paramref name="question"/> when the name is ambiguous.</summary>
    private AppRef ResolveApp(string spoken, out string? question)
    {
        question = null;
        spoken = Regex.Replace(spoken.Trim(), @"^(?:the|my|a)\s+", "").Trim();

        if (CategoryApp(spoken) is { } byCategory) return byCategory;

        var ids = _cfg.Applications.Keys.ToList();
        var q = NameMatcher.Normalize(spoken);
        if (q.Length > 0)
        {
            bool AnyName(string id, Func<string, bool> test)
            {
                var a = _cfg.Applications[id];
                return test(NameMatcher.Normalize(id)) || test(NameMatcher.Normalize(a.DisplayName))
                       || a.Aliases.Any(al => test(NameMatcher.Normalize(al)));
            }

            // exact name first; then "you said less than the full name" (open adobe -> Adobe ...); never "you said more"
            var matches = ids.Where(id => AnyName(id, n => n == q)).ToList();
            if (matches.Count == 0 && q.Length >= 3)
                matches = ids.Where(id => AnyName(id, n => n.Contains(q, StringComparison.Ordinal))).ToList();
            if (matches.Count > 1)
            {
                var starts = matches.Where(id => AnyName(id, n => n.StartsWith(q, StringComparison.Ordinal))).ToList();
                if (starts.Count == 1) matches = starts;
            }
            if (matches.Count == 1) return new AppRef(matches[0], _cfg.Applications[matches[0]].DisplayName);
            if (matches.Count > 1)
            {
                question = "Which one: " + string.Join(", ", matches.Take(3).Select(id => _cfg.Applications[id].DisplayName)) + "?";
                return new AppRef("", "");
            }
        }
        return new AppRef(spoken, spoken);      // not configured: pass the spoken name; the app resolver searches Start Menu entries
    }

    private static readonly Dictionary<string, string> CategoryWords = new(StringComparer.Ordinal)
    {
        ["browser"] = "browser", ["web browser"] = "browser", ["internet browser"] = "browser",
        ["editor"] = "editor", ["code editor"] = "editor", ["text editor"] = "editor",
        ["terminal"] = "terminal", ["command line"] = "terminal",
        ["email"] = "email", ["mail"] = "email", ["email app"] = "email", ["mail app"] = "email",
        ["music player"] = "music", ["music app"] = "music",
        ["chat app"] = "chat", ["chat"] = "chat",
        ["notes"] = "notes", ["notes app"] = "notes", ["note app"] = "notes",
        ["video player"] = "video", ["media player"] = "video",
    };

    private AppRef? CategoryApp(string spoken)
    {
        if (!CategoryWords.TryGetValue(spoken, out var category)) return null;
        if (!_cfg.DefaultApps.TryGetValue(category, out var id)) return null;
        return _cfg.Applications.TryGetValue(id, out var app) ? new AppRef(id, app.DisplayName) : null;
    }

    private string OpenPlan(string x)
    {
        x = x.Trim();
        if (x.Length == 0) return Clarify("What should I open?");

        // hints like "downloads folder", "chrome app", "bluetooth settings"
        string? hint = null;
        x = Regex.Replace(x, @"^(?:the|my|a)\s+", "");
        var hm = Regex.Match(x, @"^(.+?)\s+(folder|app|application|program|website|site|page|settings|window)$");
        if (hm.Success) { hint = hm.Groups[2].Value; x = hm.Groups[1].Value.Trim(); }
        if (hint == null)
        {
            var sm = Regex.Match(x, @"^settings\s+(?:for\s+|to\s+)?(.+)$");
            if (sm.Success) { hint = "settings"; x = sm.Groups[1].Value.Trim(); }
            else if (x == "settings") return Clarify("Which Settings page? For example: bluetooth, display, sound, Wi-Fi or Windows Update.");
            var pm = Regex.Match(x, @"^project\s+(.+)$");
            if (pm.Success) { hint = "project"; x = pm.Groups[1].Value.Trim(); }
        }

        // "open chrome and then youtube": one command at a time, never silently run just the first part
        if (Regex.IsMatch(x, @"\b(?:and|then)\b") && MatchFolder(x) == null && MatchWebsite(x) == null && MatchProject(x) == null)
            return Unsupported("Offline mode handles one command at a time.");

        if (hint == "settings")
            return MatchSettingsPage(x) is { } pg ? SettingsPlan(pg) : Clarify($"I don't have a Settings page called \"{x}\". Try bluetooth, display or sound.");

        if (hint is null or "folder" && MatchFolder(x) is { } folder)
            return Plan("open_folder", new() { ["folder"] = folder }, $"Opening {folder.Replace('_', ' ')}.");
        if (hint == "folder") return Clarify($"I don't know a folder called \"{x}\". Add it under \"folders\" in config.json.");

        if (hint is null or "project" && MatchProject(x) is { } project)
            return Plan("open_project", new() { ["project"] = project }, $"Opening the {project} project.");
        if (hint == "project") return Clarify($"I don't have a project called \"{x}\". Add it under \"projects\" in config.json.");

        if (hint is null or "website" or "site" or "page" && MatchWebsite(x) is { } site)
            return Plan("open_website", new() { ["site"] = site }, $"Opening {site}.");

        if (hint is null or "app" or "application" or "program" or "window")
        {
            var configured = ResolveApp(x, out var question);
            if (question != null) return Clarify(question);
            var known = _cfg.Applications.ContainsKey(configured.Id) || CategoryWords.ContainsKey(x);
            if (known) return Plan("open_application", new() { ["application"] = configured.Id }, $"Opening {configured.Display}.");

            // a configured name did not match: settings page, then a plain app name
            if (hint is null && MatchSettingsPage(x) is { } page) return SettingsPlan(page);
            if (Regex.IsMatch(x, @"\band\b|\bthen\b")) return Unsupported("Offline mode handles one command at a time.");
            if (Regex.IsMatch(x, @"^[\p{L}\p{N} ._\-+#()]{1,60}$") && x.Split(' ').Length <= 4)
                return Plan("open_application", new() { ["application"] = x }, $"Opening {x}.");
        }
        return Unsupported();
    }

    private string? MatchFolder(string x)
    {
        var q = NameMatcher.Normalize(x);
        if (q.Length == 0) return null;
        foreach (var f in ActionCatalog.BuiltInFolders)
            if (NameMatcher.Normalize(f) == q) return f;
        foreach (var name in _cfg.Folders.Keys)
            if (NameMatcher.Normalize(name) == q) return name;
        if (q == "my documents") return "documents";
        return null;
    }

    private string? MatchProject(string x)
    {
        var q = NameMatcher.Normalize(x);
        if (q.Length == 0) return null;
        foreach (var (name, entry) in _cfg.Projects)
            if (NameMatcher.Normalize(name) == q || entry.Aliases.Any(a => NameMatcher.Normalize(a) == q)) return name;
        return null;
    }

    private string? MatchWebsite(string x)
    {
        var q = NameMatcher.Normalize(x);
        if (q.Length == 0) return null;
        foreach (var name in _cfg.Websites.Keys)
            if (NameMatcher.Normalize(name) == q) return name;
        return null;
    }

    private static readonly Dictionary<string, string> SettingsSynonyms = new(StringComparer.Ordinal)
    {
        ["wifi"] = "wifi", ["internet"] = "network", ["audio"] = "sound", ["volume"] = "sound", ["updates"] = "windows_update",
        ["update"] = "windows_update", ["windows update"] = "windows_update", ["microphone"] = "microphone_privacy",
        ["mic"] = "microphone_privacy", ["camera"] = "camera_privacy", ["webcam"] = "camera_privacy",
        ["airplane"] = "airplane_mode", ["night light"] = "night_light", ["startup"] = "startup_apps",
    };

    private static string? MatchSettingsPage(string x)
    {
        var q = NameMatcher.Normalize(x);
        if (q.Length == 0) return null;
        foreach (var key in ActionCatalog.SettingsPages)
            if (NameMatcher.Normalize(key) == q) return key;
        foreach (var (word, page) in SettingsSynonyms)
            if (NameMatcher.Normalize(word) == q) return page;
        return null;
    }

    private static string SettingsPlan(string page) =>
        Plan("open_settings", new() { ["page"] = page }, $"Opening {page.Replace('_', ' ')} settings.");
}
