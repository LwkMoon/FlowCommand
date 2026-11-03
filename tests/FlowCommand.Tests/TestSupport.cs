using System.Net;
using System.Text;
using System.Text.Json;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Pipeline;

namespace FlowCommand.Tests;

internal static class Plan
{
    public static string Json(string action, object? parameters = null, bool confirm = false, string explanation = "Doing it.") =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = action,
            ["parameters"] = parameters ?? new { },
            ["requires_confirmation"] = confirm,
            ["explanation"] = explanation,
        });
}

internal sealed class FakeAi : IAiProvider
{
    private readonly Queue<Func<AiRequest, string>> _replies = new();
    public List<AiRequest> Requests { get; } = new();
    public string Name => "fake";

    public FakeAi Reply(string json) { _replies.Enqueue(_ => json); return this; }
    public FakeAi Throw(Exception ex) { _replies.Enqueue(_ => throw ex); return this; }

    public Task<string> GetPlanJsonAsync(AiRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_replies.Dequeue()(request));
    }
}

internal sealed class RecordingHandler : IActionHandler, IConfirmationPreview
{
    private readonly ActionResult _result;
    public RecordingHandler(ActionResult? result = null, params string[] actions)
    {
        _result = result ?? new ActionResult(true, "done");
        Actions = actions.Length > 0 ? actions : ActionCatalog.DefaultEnabled().ToArray();
    }
    public IReadOnlyCollection<string> Actions { get; }
    public List<ValidatedPlan> Executed { get; } = new();
    public bool Throw { get; set; }

    public Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext context, CancellationToken ct)
    {
        if (Throw) throw new InvalidOperationException("boom");
        Executed.Add(plan);
        return Task.FromResult(_result);
    }

    public Task<string?> PreviewAsync(ValidatedPlan plan, CancellationToken ct) => Task.FromResult<string?>("preview text");
}

internal sealed class MemorySecrets : ISecretStore
{
    private readonly Dictionary<string, string> _d = new();
    public string? Get(string name) => _d.TryGetValue(name, out var v) ? v : null;
    public void Set(string name, string value) => _d[name] = value;
    public void Delete(string name) => _d.Remove(name);
    public bool Has(string name) => _d.ContainsKey(name);
}

internal sealed class StubHttp : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, Task<HttpResponseMessage>> _fn;
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }
    public StubHttp(Func<HttpRequestMessage, string?, Task<HttpResponseMessage>> fn) => _fn = fn;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
        return await _fn(request, LastBody);
    }

    public static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static string ToolUseResponse(string inputJson) =>
        "{\"id\":\"msg_1\",\"type\":\"message\",\"role\":\"assistant\",\"stop_reason\":\"tool_use\",\"content\":[" +
        "{\"type\":\"text\",\"text\":\"ok\"}," +
        "{\"type\":\"tool_use\",\"id\":\"tu_1\",\"name\":\"submit_command\",\"input\":" + inputJson + "}]}";
}

internal static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FlowCommand.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

internal static class Cfg
{
    public static AppConfig Default() => AppConfig.CreateDefault();
}

/// <summary>Never answers; only ends when the request is cancelled (used for timeout tests).</summary>
internal sealed class HangingHttp : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(Timeout.Infinite, ct);
        return new HttpResponseMessage();
    }
}
