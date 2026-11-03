namespace FlowCommand.Core.Ai;

/// <summary>Per-provider defaults. Names are what appear in config.json ("provider").</summary>
public static class ProviderCatalog
{
    public const string Gemini = "gemini";
    public const string Anthropic = "anthropic";

    public static readonly IReadOnlyList<string> Names = new[] { Gemini, Anthropic };

    public static string Normalize(string? provider)
    {
        var p = (provider ?? "").Trim().ToLowerInvariant();
        return p.Length == 0 ? Gemini : p;
    }

    public static string DefaultModel(string? provider) => Normalize(provider) switch
    {
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
        Anthropic => "Anthropic (Claude)",
        Gemini => "Google Gemini",
        var other => other,
    };
}
