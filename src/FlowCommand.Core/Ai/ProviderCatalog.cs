namespace FlowCommand.Core.Ai;

/// <summary>Per-provider defaults. Names are what appear in config.json ("provider").</summary>
public static class ProviderCatalog
{
    /// <summary>Offline rule-based interpreter: no key, no network. The default for new installs.</summary>
    public const string Local = "local";
    public const string Gemini = "gemini";
    public const string Anthropic = "anthropic";

    public static readonly IReadOnlyList<string> Names = new[] { Local, Gemini, Anthropic };

    /// <summary>True for providers that send the dictated text to an online AI service with the user's own API key.</summary>
    public static bool UsesApiKey(string? provider) => Normalize(provider) is Gemini or Anthropic;

    public static string Normalize(string? provider)
    {
        var p = (provider ?? "").Trim().ToLowerInvariant();
        return p.Length == 0 ? Local : p;
    }

    public static string DefaultModel(string? provider) => Normalize(provider) switch
    {
        Local => "offline",
        Anthropic => "claude-haiku-5-5",
        _ => "gemini-3.5-flash-lite",
    };

    /// <summary>Environment variables checked when no key is stored (stored key wins).</summary>
    public static IReadOnlyList<string> EnvVars(string? provider) => Normalize(provider) switch
    {
        Anthropic => new[] { "ANTHROPIC_API_KEY" },
        Gemini => new[] { "GEMINI_API_KEY", "GOOGLE_API_KEY" },
        _ => Array.Empty<string>(),
    };

    public static string DisplayName(string? provider) => Normalize(provider) switch
    {
        Local => "Offline (no AI)",
        Anthropic => "Anthropic (Claude)",
        Gemini => "Google Gemini",
        var other => other,
    };
}
