using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FlowCommand.Core.Ai;

/// <summary>
/// Google Gemini API (generateContent) using a forced function call (toolConfig mode ANY restricted to one function),
/// so the model must answer with structured arguments. Only text is sent, never audio.
/// Note: unpaid Gemini API use may be used by Google to improve its products; paid use (billing enabled) is not.
/// </summary>
public sealed class GeminiProvider : IAiProvider
{
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private static readonly Regex ModelPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$", RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKey;
    private readonly TimeSpan _timeout;
    private readonly string? _thinkingLevel;
    private readonly string _baseUrl;

    /// <param name="thinkingLevel">"minimal"|"low"|"medium"|"high", or empty to leave the model default.</param>
    public GeminiProvider(HttpClient http, Func<string?> apiKey, TimeSpan timeout, string? thinkingLevel, string? baseUrl = null)
    {
        _http = http;
        _apiKey = apiKey;
        _timeout = timeout;
        _thinkingLevel = string.IsNullOrWhiteSpace(thinkingLevel) ? null : thinkingLevel.Trim().ToLowerInvariant();
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/";
    }

    public string Name => "gemini";

    public async Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new AiProviderException("No Gemini API key is configured. Open Settings from the tray icon and add one.", AiFailure.NotConfigured);

        var model = request.Model.Trim();
        if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)) model = model["models/".Length..];
        if (!ModelPattern.IsMatch(model))
            throw new AiProviderException("The model name in Settings isn't valid (letters, digits, '.', '-', '_' only).", AiFailure.NotConfigured);

        var generationConfig = new Dictionary<string, object> { ["maxOutputTokens"] = Math.Max(request.MaxTokens, 2048) };
        if (_thinkingLevel != null)
            generationConfig["thinkingConfig"] = new Dictionary<string, object> { ["thinkingLevel"] = _thinkingLevel };

        var body = new Dictionary<string, object>
        {
            ["systemInstruction"] = new Dictionary<string, object>
            {
                ["parts"] = new[] { new Dictionary<string, object> { ["text"] = request.SystemPrompt } },
            },
            ["contents"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["parts"] = new[] { new Dictionary<string, object> { ["text"] = request.UserMessage } },
                },
            },
            ["tools"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["functionDeclarations"] = new[] { ToolSchemas.ForGemini(request.Tool) },
                },
            },
            ["toolConfig"] = new Dictionary<string, object>
            {
                ["functionCallingConfig"] = new Dictionary<string, object>
                {
                    ["mode"] = "ANY",
                    ["allowedFunctionNames"] = new[] { request.Tool.Name },
                },
            },
            ["generationConfig"] = generationConfig,
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, new Uri($"{_baseUrl}{model}:generateContent"))
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        msg.Headers.Add("x-goog-api-key", key.Trim());   // header, not the URL, so the key never lands in logs/proxies

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
            return ExtractArgs(text, request.Tool.Name);
        }
    }

    private static AiProviderException MapError(HttpStatusCode status, string body)
    {
        var detail = TryGetErrorMessage(body);
        var code = (int)status;
        // Gemini reports a bad key as HTTP 400 "API key not valid", not 401.
        if (code is 401 or 403 || (detail != null && detail.Contains("API key", StringComparison.OrdinalIgnoreCase)))
            return new AiProviderException("The AI service rejected the API key. Check it in Settings.", AiFailure.Auth);
        if (code == 429)
            return new AiProviderException("AI rate limit or quota reached. Try again in a moment.", AiFailure.RateLimited);
        if (code >= 500)
            return new AiProviderException("The AI service is temporarily unavailable or overloaded. Nothing was run.", AiFailure.Unavailable);
        return new AiProviderException($"The AI service returned an error ({code}){(detail == null ? "" : ": " + detail)}", AiFailure.BadResponse);
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

    private static string ExtractArgs(string body, string toolName)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("promptFeedback", out var fb) &&
                fb.TryGetProperty("blockReason", out var br) && br.ValueKind == JsonValueKind.String)
                throw new AiProviderException($"The AI service blocked the request ({br.GetString()}). Nothing was run.", AiFailure.BadResponse);

            string? finish = null;
            if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
            {
                var cand = cands[0];
                if (cand.TryGetProperty("finishReason", out var fr) && fr.ValueKind == JsonValueKind.String) finish = fr.GetString();

                // Always iterate the parts: the function call is not guaranteed to be first.
                if (cand.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (!part.TryGetProperty("functionCall", out var fc)) continue;
                        if (!fc.TryGetProperty("name", out var n) || n.GetString() != toolName) continue;
                        if (!fc.TryGetProperty("args", out var args) || args.ValueKind != JsonValueKind.Object) continue;

                        var node = JsonNode.Parse(args.GetRawText())!.AsObject();
                        // Gemini may drop an empty "parameters" object; restore it (the validator stays strict otherwise).
                        if (!node.ContainsKey("parameters")) node["parameters"] = new JsonObject();
                        return node.ToJsonString();
                    }
                }
            }
            var why = finish == null ? "" : $" ({finish})";
            throw new AiProviderException($"The AI did not return a structured command{why}. Nothing was run.", AiFailure.BadResponse);
        }
        catch (JsonException)
        {
            throw new AiProviderException("The AI returned something unreadable. Nothing was run.", AiFailure.BadResponse);
        }
    }
}
