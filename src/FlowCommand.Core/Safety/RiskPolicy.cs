using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;

namespace FlowCommand.Core.Safety;

public sealed record PolicyDecision(bool Allowed, bool NeedsConfirmation, string Reason);

/// <summary>
/// Local, deterministic permission check. The model can ask for MORE caution (requires_confirmation=true)
/// but can never lower the risk class of an action.
/// </summary>
public static class RiskPolicy
{
    public static PolicyDecision Evaluate(ValidatedPlan plan, AppConfig cfg)
    {
        if (plan.Spec.Meta) return new(true, false, "informational only");
        if (!cfg.EnabledActions.Contains(plan.Action)) return new(false, false, "action disabled in settings");

        var risk = plan.Spec.Risk;
        if (risk == RiskLevel.High) return new(true, true, "high-risk actions always need confirmation");
        if (cfg.ConfirmAllActions) return new(true, true, "confirm-all is enabled");
        if (risk == RiskLevel.Moderate && cfg.ConfirmModerateActions) return new(true, true, "moderate actions need confirmation");
        if (plan.ModelRequestedConfirmation) return new(true, true, "the AI asked for confirmation");
        return new(true, false, "allowed");
    }
}
