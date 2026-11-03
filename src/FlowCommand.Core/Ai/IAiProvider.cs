namespace FlowCommand.Core.Ai;

public enum AiFailure { NotConfigured, Auth, RateLimited, Unavailable, BadResponse, Timeout }

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message, AiFailure kind, Exception? inner = null) : base(message, inner) => Kind = kind;
    public AiFailure Kind { get; }
}

/// <param name="Tool">Provider-neutral description of the single allowed output (each provider builds its own wire format).</param>
public sealed record AiRequest(string Model, string SystemPrompt, string UserMessage, ToolDefinition Tool, int MaxTokens = 400);

/// <summary>
/// Add a new provider by implementing this: take the request, return the raw JSON of the
/// structured plan. All validation happens locally afterwards, so providers are untrusted.
/// </summary>
public interface IAiProvider
{
    string Name { get; }
    Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct);
}

/// <summary>Used when the configured provider can't be created; fails safely with a clear message.</summary>
public sealed class UnavailableProvider : IAiProvider
{
    private readonly string _message;
    public UnavailableProvider(string name, string message) { Name = name; _message = message; }
    public string Name { get; }
    public Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct) =>
        throw new AiProviderException(_message, AiFailure.NotConfigured);
}

public static class AiProviderFactory
{
    public static IAiProvider Create(Config.AppConfig cfg, HttpClient http, Func<string?> apiKey) =>
        ProviderCatalog.Normalize(cfg.Provider) switch
        {
            ProviderCatalog.Gemini => new GeminiProvider(http, apiKey, TimeSpan.FromSeconds(cfg.RequestTimeoutSeconds), cfg.GeminiThinkingLevel),
            ProviderCatalog.Anthropic => new AnthropicProvider(http, apiKey, TimeSpan.FromSeconds(cfg.RequestTimeoutSeconds)),
            var other => new UnavailableProvider(other, $"AI provider '{other}' is not supported. Supported: {string.Join(", ", ProviderCatalog.Names)}."),
        };
}
