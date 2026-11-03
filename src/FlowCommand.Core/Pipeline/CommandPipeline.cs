using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Safety;

namespace FlowCommand.Core.Pipeline;

public enum OutcomeKind { Ready, NeedsConfirmation, Clarification, Unsupported, Rejected, Unavailable }

public sealed record Interpretation(
    OutcomeKind Kind, string Message, ValidatedPlan? Plan = null, string? UserText = null, string? Preview = null);

/// <summary>
/// text → AI (untrusted) → strict validation → local risk policy → [confirmation] → handler.
/// The AI decides what the user means; this class and the handlers decide whether and how it runs.
/// </summary>
public sealed class CommandPipeline
{
    private readonly AppConfig _cfg;
    private readonly IAiProvider _ai;
    private readonly Dictionary<string, IActionHandler> _handlers = new(StringComparer.Ordinal);
    private readonly PlanValidator _validator;
    private readonly ILog _log;

    public CommandPipeline(AppConfig cfg, IAiProvider ai, IEnumerable<IActionHandler> handlers, ILog? log = null)
    {
        _cfg = cfg;
        _ai = ai;
        _log = log ?? NullLog.Instance;
        _validator = new PlanValidator(cfg.EnabledActions);
        foreach (var h in handlers)
            foreach (var a in h.Actions) _handlers[a] = h;
        Memory = new ConversationMemory();
    }

    public ConversationMemory Memory { get; }

    public async Task<Interpretation> InterpretAsync(string? text, CancellationToken ct)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return new(OutcomeKind.Rejected, "Nothing to interpret.");
        if (text.Length > 500) text = text[..500];

        var history = _cfg.UseConversationContext ? Memory.Snapshot() : Array.Empty<ConversationTurn>();
        var request = new AiRequest(
            _cfg.Model,
            PromptBuilder.BuildSystemPrompt(_cfg),
            PromptBuilder.BuildUserMessage(text, history),
            PromptBuilder.BuildTool(_cfg));

        string raw;
        try
        {
            raw = await _ai.GetPlanJsonAsync(request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (AiProviderException ex)
        {
            _log.Warn($"AI provider failure: {ex.Kind}");
            return new(OutcomeKind.Unavailable, ex.Message, UserText: text);
        }
        catch (Exception ex)
        {
            _log.Error("Unexpected AI failure", ex);
            return new(OutcomeKind.Unavailable, "The AI service could not be used. Nothing was run.", UserText: text);
        }

        var validation = _validator.Validate(raw);
        if (!validation.Ok)
        {
            _log.Warn($"Rejected AI response: {validation.Error}");
            return new(OutcomeKind.Rejected, $"The AI's answer was not acceptable ({validation.Error}). Nothing was run.", UserText: text);
        }

        var plan = validation.Plan!;
        if (plan.Action == ActionCatalog.Clarify)
        {
            var q = plan.Arg("question");
            if (_cfg.UseConversationContext) Memory.Add(text, "asked for clarification", q);
            return new(OutcomeKind.Clarification, q, plan, text);
        }
        if (plan.Action == ActionCatalog.Unsupported)
            return new(OutcomeKind.Unsupported, "Not supported: " + plan.Arg("reason"), plan, text);

        var decision = RiskPolicy.Evaluate(plan, _cfg);
        if (!decision.Allowed)
            return new(OutcomeKind.Rejected, $"Blocked: {decision.Reason}.", plan, text);
        if (!_handlers.TryGetValue(plan.Action, out var handler))
            return new(OutcomeKind.Rejected, $"No handler is installed for '{plan.Action}'.", plan, text);

        if (decision.NeedsConfirmation)
        {
            string? preview = null;
            if (handler is IConfirmationPreview p)
            {
                try { preview = await p.PreviewAsync(plan, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { _log.Error("Preview failed", ex); }
            }
            return new(OutcomeKind.NeedsConfirmation, plan.Explanation, plan, text, preview);
        }
        return new(OutcomeKind.Ready, plan.Explanation, plan, text);
    }

    public async Task<ActionResult> ExecuteAsync(Interpretation interpretation, ActionContext context, CancellationToken ct = default)
    {
        if (interpretation.Plan == null ||
            interpretation.Kind is not (OutcomeKind.Ready or OutcomeKind.NeedsConfirmation))
            throw new InvalidOperationException("This interpretation is not executable.");

        var plan = interpretation.Plan;
        ActionResult result;

        if (_cfg.DryRun)
        {
            result = new ActionResult(true, $"[dry run] Would run {plan.Describe()} — {plan.Explanation}", Verified: false, DryRun: true);
        }
        else if (!_handlers.TryGetValue(plan.Action, out var handler))
        {
            result = ActionResult.Fail($"No handler is installed for '{plan.Action}'.");
        }
        else
        {
            try { result = await handler.ExecuteAsync(plan, context, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.Error($"Action {plan.Action} threw", ex);
                result = ActionResult.Fail($"The action failed: {ex.Message}");
            }
        }

        _log.Info($"action={plan.Action} success={result.Success} verified={result.Verified} dryRun={result.DryRun}" +
                  (_cfg.LogTranscripts ? $" text=\"{interpretation.UserText}\"" : ""));
        if (_cfg.UseConversationContext && interpretation.UserText != null)
            Memory.Add(interpretation.UserText, plan.Describe(), (result.Success ? "ok: " : "failed: ") + result.Message);
        return result;
    }
}
