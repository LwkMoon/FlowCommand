using System.Text.Json;
using System.Text.Json.Serialization;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Util;

namespace FlowCommand.Core.Config;

public sealed class ApplicationEntry
{
    public string DisplayName { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    /// <summary>e.g. "browser", "editor". Lets the AI resolve "my browser" via DefaultApps.</summary>
    public string? Category { get; set; }
    /// <summary>Process name without ".exe", used to find/close windows.</summary>
    public string? ProcessName { get; set; }
    /// <summary>Disambiguates apps sharing a process name (e.g. Opera vs Opera GX).</summary>
    public string? PathContains { get; set; }
    /// <summary>Candidate executables; %ENV% variables allowed. First existing wins.</summary>
    public List<string> Paths { get; set; } = new();
}

public sealed class ProjectEntry
{
    public string Path { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    /// <summary>Application id (from "applications") used to open the project. Default: vscode.</summary>
    public string? Editor { get; set; }
}

/// <summary>User configuration. Contains no secrets: API keys live in Windows DPAPI-protected storage.</summary>
public sealed class AppConfig
{
    /// <summary>Format version written to new files. Files without it (0) come from an older release and are migrated on load.</summary>
    public const int CurrentVersion = 2;
    public int ConfigVersion { get; set; }

    /// <summary>"gemini" (default) or "anthropic".</summary>
    public string Provider { get; set; } = ProviderCatalog.Gemini;
    public string Model { get; set; } = ProviderCatalog.DefaultModel(ProviderCatalog.Gemini);
    public int RequestTimeoutSeconds { get; set; } = 30;
    /// <summary>Gemini only: "minimal", "low", "medium", "high", or "" to use the model default. Lower = faster.</summary>
    public string GeminiThinkingLevel { get; set; } = "low";

    // ---- how the palette is opened ----
    /// <summary>Double-tapping <see cref="DoubleTapKey"/> opens the palette (a low-level listener that only looks at that one key).</summary>
    public bool DoubleTapEnabled { get; set; } = true;
    /// <summary>"backtick" (default), "capslock", "scrolllock" or "pause".</summary>
    public string DoubleTapKey { get; set; } = TriggerKeys.Default.Id;
    /// <summary>Old name of <see cref="DoubleTapEnabled"/>; read only so existing config files keep working (migrated on load).</summary>
    [JsonPropertyName("doubleTapCapsLock")]
    public bool? LegacyDoubleTapCapsLock { get; set; }
    public int DoubleTapIntervalMs { get; set; } = 400;
    /// <summary>If a text box has focus, the double-tap is ignored so normal Wispr dictation is untouched.</summary>
    public bool IgnoreWhenTextBoxFocused { get; set; } = true;
    /// <summary>When focus can't be classified (e.g. a browser that hasn't exposed accessibility yet), open anyway.</summary>
    public bool OpenWhenFocusUnknown { get; set; } = true;
    /// <summary>Processes whose accessibility tree is built lazily (browsers, Electron apps).</summary>
    public List<string> WebContentProcesses { get; set; } = new()
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "code", "cursor", "slack", "discord", "teams", "notion", "obsidian",
    };
    /// <summary>Optional extra shortcut, e.g. "Ctrl+Alt+Space". Always opens the palette, even with a text box focused. Empty = none.</summary>
    public string Hotkey { get; set; } = "";
    /// <summary>"paste" (default), "idle" or "off".</summary>
    public string AutoSubmitMode { get; set; } = "paste";
    public int AutoSubmitDelayMs { get; set; } = 900;
    public int PasteThresholdChars { get; set; } = 4;
    public bool ReclaimFocusAfterAction { get; set; } = true;

    public bool DryRun { get; set; }
    public bool ConfirmModerateActions { get; set; }
    public bool ConfirmAllActions { get; set; }
    public bool UseConversationContext { get; set; } = true;
    public bool LogTranscripts { get; set; }

    public List<string> EnabledActions { get; set; } = ActionCatalog.DefaultEnabled().ToList();

    // ---- websites and web search ----
    /// <summary>Name spoken -> http(s) URL. Only these names can be opened directly; anything else becomes a search.</summary>
    public Dictionary<string, string> Websites { get; set; } = DefaultCatalog.Websites();
    /// <summary>Search URL template: https only, must contain {query}.</summary>
    public string SearchUrl { get; set; } = WebTargets.DefaultSearchUrl;
    /// <summary>"" opens websites in the Windows default browser; otherwise an application id from "applications".</summary>
    public string WebsiteBrowser { get; set; } = "";

    public Dictionary<string, string> DefaultApps { get; set; } = DefaultCatalog.DefaultApps();
    public Dictionary<string, ApplicationEntry> Applications { get; set; } = DefaultCatalog.Applications();
    public Dictionary<string, ProjectEntry> Projects { get; set; } = new();
    public Dictionary<string, string> Folders { get; set; } = new();

    public static AppConfig CreateDefault() => new() { ConfigVersion = CurrentVersion };

    public AppConfig Clone() =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, ConfigStore.Options), ConfigStore.Options)!;

    /// <summary>Human-readable description of how to open the palette, for hints.</summary>
    public string TriggerDescription()
    {
        var parts = new List<string>();
        if (DoubleTapEnabled) parts.Add("double-tap " + (TriggerKeys.Parse(DoubleTapKey) ?? TriggerKeys.Default).Display);
        if (!string.IsNullOrWhiteSpace(Hotkey)) parts.Add(Hotkey);
        return parts.Count == 0 ? "(no trigger enabled)" : string.Join(" or ", parts);
    }
}
