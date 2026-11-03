using System.Net;
using System.Text.Json;
using FlowCommand.Core.Ai;
using Xunit;

namespace FlowCommand.Tests;

public class AnthropicProviderTests
{
    private static AiRequest Req() =>
        new("claude-haiku-5-5", "system", "user text", PromptBuilder.BuildTool(Cfg.Default()));

    private static AnthropicProvider Provider(HttpMessageHandler stub, string? key = "sk-test", TimeSpan? timeout = null) =>
        new(new HttpClient(stub), () => key, timeout ?? TimeSpan.FromSeconds(5), "https://example.invalid/v1/messages");

    [Fact]
    public async Task Returns_tool_input_json_and_sends_expected_request()
    {
        var input = Plan.Json("open_application", new { application = "chrome" });
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(HttpStatusCode.OK, StubHttp.ToolUseResponse(input))));

        var raw = await Provider(stub).GetPlanJsonAsync(Req(), default);

        Assert.Equal("chrome", JsonDocument.Parse(raw).RootElement.GetProperty("parameters").GetProperty("application").GetString());
        Assert.Equal("sk-test", stub.LastRequest!.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", stub.LastRequest.Headers.GetValues("anthropic-version").Single());

        using var body = JsonDocument.Parse(stub.LastBody!);
        var root = body.RootElement;
        Assert.Equal("claude-haiku-5-5", root.GetProperty("model").GetString());
        Assert.Equal("tool", root.GetProperty("tool_choice").GetProperty("type").GetString());
        Assert.Equal("submit_command", root.GetProperty("tool_choice").GetProperty("name").GetString());
        Assert.Equal("user text", root.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Missing_key_fails_before_any_network_call()
    {
        var called = false;
        var stub = new StubHttp((_, _) => { called = true; return Task.FromResult(StubHttp.Json(HttpStatusCode.OK, "{}")); });
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Provider(stub, key: "  ").GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.NotConfigured, ex.Kind);
        Assert.False(called);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, AiFailure.Auth)]
    [InlineData(HttpStatusCode.TooManyRequests, AiFailure.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, AiFailure.BadResponse)]
    public async Task Http_errors_are_mapped(HttpStatusCode code, AiFailure expected)
    {
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(code, "{\"error\":{\"message\":\"nope\"}}")));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Provider(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task Response_without_tool_use_is_rejected()
    {
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(HttpStatusCode.OK,
            "{\"content\":[{\"type\":\"text\",\"text\":\"Sure, I opened it!\"}]}")));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Provider(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.BadResponse, ex.Kind);
    }

    [Fact]
    public async Task Garbage_response_body_is_rejected()
    {
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(HttpStatusCode.OK, "<html>oops</html>")));
        await Assert.ThrowsAsync<AiProviderException>(() => Provider(stub).GetPlanJsonAsync(Req(), default));
    }

    [Fact]
    public async Task Network_failure_is_reported_as_unavailable()
    {
        var stub = new StubHttp((_, _) => throw new HttpRequestException("no route"));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Provider(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Slow_service_times_out()
    {
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => Provider(new HangingHttp(), timeout: TimeSpan.FromMilliseconds(100)).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_timeout()
    {
        using var cts = new CancellationTokenSource();
        var task = Provider(new HangingHttp()).GetPlanJsonAsync(Req(), cts.Token);
        cts.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Factory_returns_a_failing_provider_for_unknown_names()
    {
        var cfg = Cfg.Default();
        cfg.Provider = "mystery";
        var p = AiProviderFactory.Create(cfg, new HttpClient(), () => "k");
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => p.GetPlanJsonAsync(Req(), default));
        Assert.Contains("mystery", ex.Message);
    }

    [Fact]
    public void Factory_creates_anthropic_when_asked()
    {
        var cfg = Cfg.Default();
        cfg.Provider = "anthropic";
        Assert.Equal("anthropic", AiProviderFactory.Create(cfg, new HttpClient(), () => "k").Name);
    }
}
