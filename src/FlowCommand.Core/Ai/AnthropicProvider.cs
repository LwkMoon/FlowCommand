using System.Net;
using System.Text;
using System.Text.Json;

namespace FlowCommand.Core.Ai;

/// <summary>
/// Anthropic Messages API using a forced tool call, so the model must return structured JSON.
/// Only text is sent: the system prompt, a few recent commands and the dictated text. Never audio.
/// API usage is billed separately from a Claude.ai subscription.
/// </summary>
public sealed class AnthropicProvider : IAiProvider
{
    public const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";
    public const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKey;
    private readonly TimeSpan _timeout;
    private readonly Uri _endpoint;

    public AnthropicProvider(HttpClient http, Func<string?> apiKey, TimeSpan timeout, string? endpoint = null)
    {
        _http = http;
        _apiKey = apiKey;
        _timeout = timeout;
        _endpoint = new Uri(endpoint ?? DefaultEndpoint);
    }

    public string Name => "anthropic";

    public async Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new AiProviderException("No Anthropic API key is configured. Open Settings from the tray icon and add one.", AiFailure.NotConfigured);

        var body = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["system"] = request.SystemPrompt,
            ["messages"] = new[] { new Dictionary<string, object> { ["role"] = "user", ["content"] = request.UserMessage } },
            ["tools"] = new[] { ToolSchemas.ForAnthropic(request.Tool) },
            ["tool_choice"] = new Dictionary<string, object> { ["type"] = "tool", ["name"] = request.Tool.Name },
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("x-api-key", key.Trim());
        msg.Headers.Add("anthropic-version", ApiVersion);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        HttpResponseMessage resp;
        string text;
        try
        {
            resp = await _http.SendAsync(msg, cts.Token).ConfigureAwait(false);
            text = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException("The AI service timed out. Nothing was run.", AiFailure.Timeout);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException("Couldn't reach the AI service. Check your connection. Nothing was run.", AiFailure.Unavailable, ex);
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode) throw MapError(resp.StatusCode, text);
            return ExtractToolInput(text, request.Tool.Name);
        }
    }

    private static AiProviderException MapError(HttpStatusCode status, string body)
    {
        var detail = TryGetErrorMessage(body);
        return (int)status switch
        {
            401 or 403 => new AiProviderException("The AI service rejected the API key. Check it in Settings.", AiFailure.Auth),
            429 => new AiProviderException("AI rate limit reached. Try again in a moment.", AiFailure.RateLimited),
            >= 500 => new AiProviderException("The AI service is temporarily unavailable. Nothing was run.", AiFailure.Unavailable),
            _ => new AiProviderException($"The AI service returned an error ({(int)status}){(detail == null ? "" : ": " + detail)}", AiFailure.BadResponse),
        };
    }

    private static string? TryGetErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) &&
                err.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            {
                var s = m.GetString() ?? "";
                return s.Length > 200 ? s[..200] : s;
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static string ExtractToolInput(string body, string toolName)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var t) && t.GetString() == "tool_use" &&
                        block.TryGetProperty("name", out var n) && n.GetString() == toolName &&
                        block.TryGetProperty("input", out var input))
                        return input.GetRawText();
                }
            }
        }
        catch (JsonException) { }
        throw new AiProviderException("The AI did not return a structured command. Nothing was run.", AiFailure.BadResponse);
    }
}
