using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Pipeline;
using Xunit;

namespace FlowCommand.Tests;

public class PipelineTests
{
    private static readonly ActionContext Ctx = new(0);

    private static (CommandPipeline pipe, FakeAi ai, RecordingHandler handler, AppConfig cfg) Make(Action<AppConfig>? tweak = null)
    {
        var cfg = Cfg.Default();
        tweak?.Invoke(cfg);
        var ai = new FakeAi();
        var handler = new RecordingHandler();
        return (new CommandPipeline(cfg, ai, new[] { handler }), ai, handler, cfg);
    }

    [Fact]
    public async Task Natural_language_is_interpreted_and_executed()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Reply(Plan.Json("open_application", new { application = "chrome" }, false, "Opening Google Chrome."));

        var i = await pipe.InterpretAsync("I need my browser", default);
        Assert.Equal(OutcomeKind.Ready, i.Kind);
        var r = await pipe.ExecuteAsync(i, Ctx);

        Assert.True(r.Success);
        Assert.Single(handler.Executed);
        Assert.Equal("chrome", handler.Executed[0].Arg("application"));
    }

    [Fact]
    public async Task Dry_run_interprets_but_never_executes()
    {
        var (pipe, ai, handler, _) = Make(c => c.DryRun = true);
        ai.Reply(Plan.Json("show_desktop"));

        var i = await pipe.InterpretAsync("take me back to the desktop", default);
        var r = await pipe.ExecuteAsync(i, Ctx);

        Assert.True(r.DryRun);
        Assert.Empty(handler.Executed);
        Assert.Contains("dry run", r.Message);
    }

    [Fact]
    public async Task Ai_outage_fails_safely_without_executing()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Throw(new AiProviderException("down", AiFailure.Unavailable));

        var i = await pipe.InterpretAsync("open chrome", default);

        Assert.Equal(OutcomeKind.Unavailable, i.Kind);
        Assert.Null(i.Plan);
        Assert.Empty(handler.Executed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => pipe.ExecuteAsync(i, Ctx));
    }

    [Fact]
    public async Task Unexpected_provider_exception_also_fails_safely()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Throw(new InvalidOperationException("kaboom"));
        var i = await pipe.InterpretAsync("open chrome", default);
        Assert.Equal(OutcomeKind.Unavailable, i.Kind);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public async Task Cancellation_propagates()
    {
        var (pipe, ai, _, _) = Make();
        ai.Throw(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipe.InterpretAsync("open chrome", default));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("{\"action\":\"run_powershell\",\"parameters\":{\"script\":\"x\"},\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"open_application\",\"parameters\":{},\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    public async Task Invalid_ai_output_is_rejected_and_nothing_runs(string raw)
    {
        var (pipe, ai, handler, _) = Make();
        ai.Reply(raw);
        var i = await pipe.InterpretAsync("do something", default);
        Assert.Equal(OutcomeKind.Rejected, i.Kind);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public async Task Ambiguity_produces_a_clarification_and_executes_nothing()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Reply(Plan.Json("clarify", new { question = "Chrome or Opera GX?" }));
        var i = await pipe.InterpretAsync("open the browser", default);
        Assert.Equal(OutcomeKind.Clarification, i.Kind);
        Assert.Equal("Chrome or Opera GX?", i.Message);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public async Task Unsupported_requests_are_reported_honestly()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Reply(Plan.Json("unsupported", new { reason = "I can't send email." }));
        var i = await pipe.InterpretAsync("email my boss", default);
        Assert.Equal(OutcomeKind.Unsupported, i.Kind);
        Assert.Contains("can't send email", i.Message);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public async Task High_risk_requires_confirmation_and_shows_a_preview()
    {
        var (pipe, ai, handler, _) = Make();
        ai.Reply(Plan.Json("kill_process_on_port", new { port = 3000 }, confirm: false, "Killing whatever is on port 3000."));

        var i = await pipe.InterpretAsync("kill whatever is on port 3000", default);

        Assert.Equal(OutcomeKind.NeedsConfirmation, i.Kind);
        Assert.Equal("preview text", i.Preview);
        Assert.Empty(handler.Executed);          // nothing ran yet

        await pipe.ExecuteAsync(i, Ctx);         // person pressed Enter
        Assert.Single(handler.Executed);
    }

    [Fact]
    public async Task Disabled_action_is_rejected_by_validator()
    {
        var (pipe, ai, handler, _) = Make(c => c.EnabledActions.Remove("bluetooth_set"));
        ai.Reply(Plan.Json("bluetooth_set", new { state = "off" }));
        var i = await pipe.InterpretAsync("turn bluetooth off", default);
        Assert.Equal(OutcomeKind.Rejected, i.Kind);
        Assert.Empty(handler.Executed);
    }

    [Fact]
    public async Task Missing_handler_is_rejected()
    {
        var cfg = Cfg.Default();
        var ai = new FakeAi().Reply(Plan.Json("show_desktop"));
        var pipe = new CommandPipeline(cfg, ai, Array.Empty<IActionHandler>());
        var i = await pipe.InterpretAsync("show desktop", default);
        Assert.Equal(OutcomeKind.Rejected, i.Kind);
    }

    [Fact]
    public async Task Handler_exception_becomes_a_failed_result_not_a_crash()
    {
        var (pipe, ai, handler, _) = Make();
        handler.Throw = true;
        ai.Reply(Plan.Json("show_desktop"));
        var i = await pipe.InterpretAsync("show desktop", default);
        var r = await pipe.ExecuteAsync(i, Ctx);
        Assert.False(r.Success);
        Assert.Contains("boom", r.Message);
    }

    [Fact]
    public async Task Empty_text_is_not_sent_to_the_ai()
    {
        var (pipe, ai, _, _) = Make();
        var i = await pipe.InterpretAsync("   ", default);
        Assert.Equal(OutcomeKind.Rejected, i.Kind);
        Assert.Empty(ai.Requests);
    }

    [Fact]
    public async Task Follow_up_correction_receives_recent_history()
    {
        var (pipe, ai, _, _) = Make();
        ai.Reply(Plan.Json("open_application", new { application = "chrome" }));
        ai.Reply(Plan.Json("open_application", new { application = "operagx" }));

        var first = await pipe.InterpretAsync("open Chrome", default);
        await pipe.ExecuteAsync(first, Ctx);
        await pipe.InterpretAsync("actually, open the other browser instead", default);

        var second = ai.Requests[1].UserMessage;
        Assert.Contains("open Chrome", second);
        Assert.Contains("open_application(application=chrome)", second);
        Assert.Contains("actually, open the other browser instead", second);
        Assert.DoesNotContain("actually", ai.Requests[0].UserMessage);
    }

    [Fact]
    public async Task History_is_not_sent_when_context_is_disabled()
    {
        var (pipe, ai, _, _) = Make(c => c.UseConversationContext = false);
        ai.Reply(Plan.Json("show_desktop")).Reply(Plan.Json("show_desktop"));
        var first = await pipe.InterpretAsync("show desktop one", default);
        await pipe.ExecuteAsync(first, Ctx);
        await pipe.InterpretAsync("show desktop two", default);
        Assert.DoesNotContain("show desktop one", ai.Requests[1].UserMessage);
        Assert.Empty(pipe.Memory.Snapshot());
    }

    [Fact]
    public async Task Clarification_answer_is_interpreted_in_context()
    {
        var (pipe, ai, _, _) = Make();
        ai.Reply(Plan.Json("clarify", new { question = "Which browser?" }));
        ai.Reply(Plan.Json("open_application", new { application = "operagx" }));
        await pipe.InterpretAsync("open the browser", default);
        await pipe.InterpretAsync("opera", default);
        Assert.Contains("Which browser?", ai.Requests[1].UserMessage);
    }

    [Fact]
    public void Memory_entries_expire()
    {
        var now = DateTimeOffset.UtcNow;
        var mem = new ConversationMemory(4, TimeSpan.FromMinutes(10), () => now);
        mem.Add("old", "a", "r");
        now += TimeSpan.FromMinutes(11);
        mem.Add("new", "a", "r");
        Assert.Single(mem.Snapshot());
        Assert.Equal("new", mem.Snapshot()[0].UserText);
    }

    [Fact]
    public void Memory_is_capped()
    {
        var mem = new ConversationMemory(3);
        for (var i = 0; i < 10; i++) mem.Add("t" + i, "a", "r");
        Assert.Equal(3, mem.Snapshot().Count);
        Assert.Equal("t9", mem.Snapshot()[2].UserText);
    }
}
