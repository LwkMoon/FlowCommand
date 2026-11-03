using FlowCommand.Core.Actions;
using FlowCommand.Core.Safety;
using Xunit;

namespace FlowCommand.Tests;

public class RiskPolicyTests
{
    private static ValidatedPlan P(string action, object? args = null, bool modelConfirm = false)
    {
        var r = new PlanValidator(ActionCatalog.DefaultEnabled()).Validate(Plan.Json(action, args, modelConfirm));
        Assert.True(r.Ok, r.Error);
        return r.Plan!;
    }

    [Fact]
    public void Low_risk_runs_without_confirmation()
    {
        var d = RiskPolicy.Evaluate(P("open_application", new { application = "chrome" }), Cfg.Default());
        Assert.True(d.Allowed);
        Assert.False(d.NeedsConfirmation);
    }

    [Fact]
    public void High_risk_always_needs_confirmation_even_if_model_says_no()
    {
        var d = RiskPolicy.Evaluate(P("kill_process_on_port", new { port = 3000 }, modelConfirm: false), Cfg.Default());
        Assert.True(d.Allowed);
        Assert.True(d.NeedsConfirmation);
    }

    [Fact]
    public void Model_can_raise_but_not_lower_caution()
    {
        var raised = RiskPolicy.Evaluate(P("show_desktop", null, modelConfirm: true), Cfg.Default());
        Assert.True(raised.NeedsConfirmation);
    }

    [Fact]
    public void Moderate_risk_follows_setting()
    {
        var cfg = Cfg.Default();
        var plan = P("close_application", new { application = "chrome" });
        Assert.False(RiskPolicy.Evaluate(plan, cfg).NeedsConfirmation);
        cfg.ConfirmModerateActions = true;
        Assert.True(RiskPolicy.Evaluate(plan, cfg).NeedsConfirmation);
    }

    [Fact]
    public void Confirm_all_applies_to_low_risk()
    {
        var cfg = Cfg.Default();
        cfg.ConfirmAllActions = true;
        Assert.True(RiskPolicy.Evaluate(P("show_desktop"), cfg).NeedsConfirmation);
    }

    [Fact]
    public void Disabled_actions_are_blocked()
    {
        var cfg = Cfg.Default();
        cfg.EnabledActions.Remove("bluetooth_set");
        var d = RiskPolicy.Evaluate(P("bluetooth_set", new { state = "off" }), cfg);
        Assert.False(d.Allowed);
    }

    [Fact]
    public void Risk_classes_match_the_specification()
    {
        Assert.Equal(RiskLevel.Low, ActionCatalog.Find("open_application")!.Risk);
        Assert.Equal(RiskLevel.Low, ActionCatalog.Find("show_desktop")!.Risk);
        Assert.Equal(RiskLevel.Low, ActionCatalog.Find("open_folder")!.Risk);
        Assert.Equal(RiskLevel.Moderate, ActionCatalog.Find("close_application")!.Risk);
        Assert.Equal(RiskLevel.Moderate, ActionCatalog.Find("bluetooth_set")!.Risk);
        Assert.Equal(RiskLevel.Moderate, ActionCatalog.Find("inspect_port")!.Risk);
        Assert.Equal(RiskLevel.High, ActionCatalog.Find("kill_process_on_port")!.Risk);
    }

    [Fact]
    public void Catalog_has_no_shell_or_file_deletion_capability()
    {
        var names = ActionCatalog.All.Select(a => a.Name).ToList();
        Assert.DoesNotContain(names, n => n.Contains("shell") || n.Contains("powershell") || n.Contains("delete") ||
                                          n.Contains("shutdown") || n.Contains("restart") || n.Contains("exec") || n.Contains("script"));
    }
}
