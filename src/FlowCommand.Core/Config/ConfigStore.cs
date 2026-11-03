using System.Text.Json;
using System.Text.Json.Serialization;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Util;

namespace FlowCommand.Core.Config;

public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class ConfigStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Loads the config; creates a default file when none exists.</summary>
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var def = AppConfig.CreateDefault();
            Save(path, def);
            return def;
        }
        try
        {
            var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options)
                      ?? throw new ConfigException("config.json is empty.");
            return Normalize(cfg);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"config.json is not valid: {ex.Message}", ex);
        }
    }

    public static void Save(string path, AppConfig cfg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Normalize(cfg), Options));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Clamps values and repairs nulls so a hand-edited file can't crash the app.</summary>
    public static AppConfig Normalize(AppConfig c)
    {
        c.Provider = ProviderCatalog.Normalize(c.Provider);
        if (string.IsNullOrWhiteSpace(c.Model)) c.Model = ProviderCatalog.DefaultModel(c.Provider);
        c.Hotkey = (c.Hotkey ?? "").Trim();
        c.GeminiThinkingLevel = (c.GeminiThinkingLevel ?? "").Trim().ToLowerInvariant();
        if (c.GeminiThinkingLevel is not ("" or "minimal" or "low" or "medium" or "high")) c.GeminiThinkingLevel = "low";
        if (c.LegacyDoubleTapCapsLock.HasValue)            // config written by an older version
        {
            c.DoubleTapEnabled = c.LegacyDoubleTapCapsLock.Value;
            c.LegacyDoubleTapCapsLock = null;
        }
        c.DoubleTapKey = (TriggerKeys.Parse(c.DoubleTapKey) ?? TriggerKeys.Default).Id;
        c.DoubleTapIntervalMs = Math.Clamp(c.DoubleTapIntervalMs, 150, 1000);
        c.WebContentProcesses ??= new();
        c.AutoSubmitMode = (c.AutoSubmitMode ?? "paste").Trim().ToLowerInvariant();
        if (c.AutoSubmitMode is not ("off" or "paste" or "idle")) c.AutoSubmitMode = "paste";
        c.AutoSubmitDelayMs = Math.Clamp(c.AutoSubmitDelayMs, 200, 5000);
        c.PasteThresholdChars = Math.Clamp(c.PasteThresholdChars, 2, 50);
        c.RequestTimeoutSeconds = Math.Clamp(c.RequestTimeoutSeconds, 5, 60);

        var known = ActionCatalog.All.Where(a => !a.Meta).Select(a => a.Name).ToHashSet();
        c.EnabledActions = (c.EnabledActions ?? new()).Where(known.Contains).Distinct().ToList();
        c.Websites ??= new();
        c.WebsiteBrowser = (c.WebsiteBrowser ?? "").Trim();
        if (!WebTargets.TryBuildSearchUrl(c.SearchUrl, "test", out _)) c.SearchUrl = WebTargets.DefaultSearchUrl;
        if (c.ConfigVersion < 2)    // written before websites, media keys and screenshots existed: switch on what is new
        {
            foreach (var action in ActionCatalog.AddedInConfigVersion2)
                if (!c.EnabledActions.Contains(action)) c.EnabledActions.Add(action);
        }
        c.ConfigVersion = AppConfig.CurrentVersion;
        c.DefaultApps ??= new();
        c.Applications ??= new();
        c.Projects ??= new();
        c.Folders ??= new();
        foreach (var a in c.Applications.Values) { a.Aliases ??= new(); a.Paths ??= new(); }
        foreach (var p in c.Projects.Values) p.Aliases ??= new();
        return c;
    }
}
