using System.Net;
using System.Text.Json;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using Xunit;

namespace FlowCommand.Tests;

public class GeminiProviderTests
{
    private const string BaseUrl = "https://example.invalid/v1beta/models";

    private static GeminiProvider P(HttpMessageHandler h, string? key = "g-test", TimeSpan? timeout = null, string? think = "low") =>
        new(new HttpClient(h), () => key, timeout ?? TimeSpan.FromSeconds(5), think, BaseUrl);

    private static AiRequest Req(string model = "gemini-3.8-flash") =>
        new(model, "system text", "user text", PromptBuilder.BuildTool(Cfg.Default()));

    private static string CallResponse(string argsJson, string name = "submit_command") =>
        "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"thinking out loud\"}," +
        "{\"functionCall\":{\"name\":\"" + name + "\",\"args\":" + argsJson + ",\"id\":\"c1\"}}]},\"finishReason\":\"STOP\"}]}";

    private static StubHttp Ok(string body) => new((_, _) => Task.FromResult(StubHttp.Json(HttpStatusCode.OK, body)));

    [Fact]
    public async Task Sends_the_expected_request_shape()
    {
        var stub = Ok(CallResponse(Plan.Json("show_desktop")));
        await P(stub).GetPlanJsonAsync(Req(), default);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.EndsWith("/gemini-3.8-flash:generateContent", uri.AbsolutePath);
        Assert.Equal("", uri.Query);                                            // key must not be in the URL
        Assert.Equal("g-test", stub.LastRequest.Headers.GetValues("x-goog-api-key").Single());

        using var doc = JsonDocument.Parse(stub.LastBody!);
        var r = doc.RootElement;
        Assert.Equal("system text", r.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("user", r.GetProperty("contents")[0].GetProperty("role").GetString());
        Assert.Equal("user text", r.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());

        var fcc = r.GetProperty("toolConfig").GetProperty("functionCallingConfig");
        Assert.Equal("ANY", fcc.GetProperty("mode").GetString());
        Assert.Equal("submit_command", fcc.GetProperty("allowedFunctionNames")[0].GetString());

        var decl = r.GetProperty("tools")[0].GetProperty("functionDeclarations")[0];
        Assert.Equal("submit_command", decl.GetProperty("name").GetString());
        Assert.True(r.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32() >= 2048);
        Assert.Equal("low", r.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Fact]
    public async Task Omits_thinking_config_when_not_set()
    {
        var stub = Ok(CallResponse(Plan.Json("show_desktop")));
        await P(stub, think: "").GetPlanJsonAsync(Req(), default);
        using var doc = JsonDocument.Parse(stub.LastBody!);
        Assert.False(doc.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
    }

    [Fact]
    public async Task Request_body_never_contains_the_api_key()
    {
        var stub = Ok(CallResponse(Plan.Json("show_desktop")));
        await P(stub, key: "super-secret-key").GetPlanJsonAsync(Req(), default);
        Assert.DoesNotContain("super-secret-key", stub.LastBody!);
    }

    [Fact]
    public async Task Finds_the_function_call_even_when_it_is_not_the_first_part_and_the_result_validates()
    {
        var raw = await P(Ok(CallResponse(Plan.Json("open_application", new { application = "chrome" })))).GetPlanJsonAsync(Req(), default);
        var v = new PlanValidator(ActionCatalog.DefaultEnabled()).Validate(raw);
        Assert.True(v.Ok, v.Error);
        Assert.Equal("chrome", v.Plan!.Arg("application"));
    }

    [Fact]
    public async Task Restores_an_omitted_empty_parameters_object()
    {
        const string args = "{\"action\":\"show_desktop\",\"requires_confirmation\":false,\"explanation\":\"Showing the desktop.\"}";
        var raw = await P(Ok(CallResponse(args))).GetPlanJsonAsync(Req(), default);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(raw).RootElement.GetProperty("parameters").ValueKind);
        Assert.True(new PlanValidator(ActionCatalog.DefaultEnabled()).Validate(raw).Ok);
    }

    [Fact]
    public async Task Does_not_invent_other_missing_fields()
    {
        const string args = "{\"action\":\"show_desktop\",\"parameters\":{}}";   // no requires_confirmation / explanation
        var raw = await P(Ok(CallResponse(args))).GetPlanJsonAsync(Req(), default);
        Assert.False(new PlanValidator(ActionCatalog.DefaultEnabled()).Validate(raw).Ok);
    }

    [Fact]
    public async Task Missing_key_fails_before_any_network_call()
    {
        var called = false;
        var stub = new StubHttp((_, _) => { called = true; return Task.FromResult(StubHttp.Json(HttpStatusCode.OK, "{}")); });
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub, key: " ").GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.NotConfigured, ex.Kind);
        Assert.False(called);
    }

    [Fact]
    public async Task Models_prefix_is_accepted_and_odd_model_names_are_refused()
    {
        var stub = Ok(CallResponse(Plan.Json("show_desktop")));
        await P(stub).GetPlanJsonAsync(Req("models/gemini-3.8-flash"), default);
        Assert.EndsWith("/gemini-3.8-flash:generateContent", stub.LastRequest!.RequestUri!.AbsolutePath);

        foreach (var bad in new[] { "../evil", "..", ".hidden", "a b", "x?key=1", "m:generate", "" })
        {
            var called = false;
            var s = new StubHttp((_, _) => { called = true; return Task.FromResult(StubHttp.Json(HttpStatusCode.OK, "{}")); });
            await Assert.ThrowsAsync<AiProviderException>(() => P(s).GetPlanJsonAsync(Req(bad), default));
            Assert.False(called);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "API key not valid. Please pass a valid API key.", AiFailure.Auth)]
    [InlineData(HttpStatusCode.Forbidden, "denied", AiFailure.Auth)]
    [InlineData(HttpStatusCode.Unauthorized, "nope", AiFailure.Auth)]
    [InlineData(HttpStatusCode.TooManyRequests, "quota", AiFailure.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "overloaded", AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, "oops", AiFailure.Unavailable)]
    [InlineData(HttpStatusCode.NotFound, "models/x is not found", AiFailure.BadResponse)]
    [InlineData(HttpStatusCode.BadRequest, "Invalid JSON payload", AiFailure.BadResponse)]
    public async Task Http_errors_are_mapped(HttpStatusCode code, string message, AiFailure expected)
    {
        var body = JsonSerializer.Serialize(new { error = new { code = (int)code, message, status = "X" } });
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(code, body)));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(expected, ex.Kind);
    }

    [Fact]
    public async Task Not_found_includes_the_services_explanation_so_a_bad_model_name_is_diagnosable()
    {
        var stub = new StubHttp((_, _) => Task.FromResult(StubHttp.Json(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"models/zzz is not found\"}}")));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req("zzz"), default));
        Assert.Contains("models/zzz is not found", ex.Message);
    }

    [Fact]
    public async Task Blocked_prompt_is_reported_and_nothing_is_returned()
    {
        var stub = Ok("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}");
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
        Assert.Contains("blocked", ex.Message);
    }

    [Fact]
    public async Task Malformed_function_call_finish_reason_is_reported()
    {
        var stub = Ok("{\"candidates\":[{\"finishReason\":\"MALFORMED_FUNCTION_CALL\"}]}");
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
        Assert.Contains("MALFORMED_FUNCTION_CALL", ex.Message);
    }

    [Fact]
    public async Task Text_only_answer_is_rejected()
    {
        var stub = Ok("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Done, I opened Chrome.\"}]},\"finishReason\":\"STOP\"}]}");
        await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
    }

    [Fact]
    public async Task Function_call_with_a_different_name_is_ignored()
    {
        var stub = Ok(CallResponse(Plan.Json("show_desktop"), name: "something_else"));
        await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
    }

    [Fact]
    public async Task Garbage_body_is_rejected()
    {
        var stub = Ok("<html>nope</html>");
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.BadResponse, ex.Kind);
    }

    [Fact]
    public async Task Network_failure_is_reported_as_unavailable()
    {
        var stub = new StubHttp((_, _) => throw new HttpRequestException("no route"));
        var ex = await Assert.ThrowsAsync<AiProviderException>(() => P(stub).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.Unavailable, ex.Kind);
    }

    [Fact]
    public async Task Slow_service_times_out()
    {
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => P(new HangingHttp(), timeout: TimeSpan.FromMilliseconds(100)).GetPlanJsonAsync(Req(), default));
        Assert.Equal(AiFailure.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_timeout()
    {
        using var cts = new CancellationTokenSource();
        var task = P(new HangingHttp()).GetPlanJsonAsync(Req(), cts.Token);
        cts.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Factory_defaults_to_gemini_and_still_supports_anthropic()
    {
        Assert.Equal("gemini", AiProviderFactory.Create(Cfg.Default(), new HttpClient(), () => "k").Name);
        var cfg = Cfg.Default();
        cfg.Provider = "anthropic";
        Assert.Equal("anthropic", AiProviderFactory.Create(cfg, new HttpClient(), () => "k").Name);
    }
}
