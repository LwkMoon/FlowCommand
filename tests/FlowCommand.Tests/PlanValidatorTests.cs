using FlowCommand.Core.Actions;
using Xunit;

namespace FlowCommand.Tests;

public class PlanValidatorTests
{
    private static PlanValidator V() => new(ActionCatalog.DefaultEnabled());

    [Fact]
    public void Accepts_valid_open_application()
    {
        var r = V().Validate(Plan.Json("open_application", new { application = "chrome" }, false, "Opening Google Chrome."));
        Assert.True(r.Ok, r.Error);
        Assert.Equal("open_application", r.Plan!.Action);
        Assert.Equal("chrome", r.Plan.Arg("application"));
        Assert.Equal("Opening Google Chrome.", r.Plan.Explanation);
    }

    [Fact]
    public void Accepts_action_with_no_parameters()
    {
        Assert.True(V().Validate(Plan.Json("show_desktop")).Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"open chrome\"")]
    [InlineData("{")]
    public void Rejects_malformed_or_non_object(string raw) => Assert.False(V().Validate(raw).Ok);

    [Fact]
    public void Rejects_unknown_action() =>
        Assert.False(V().Validate(Plan.Json("run_powershell", new { script = "rm -rf /" })).Ok);

    [Fact]
    public void Rejects_disabled_action()
    {
        var v = new PlanValidator(new[] { "show_desktop" });
        var r = v.Validate(Plan.Json("open_application", new { application = "chrome" }));
        Assert.False(r.Ok);
        Assert.Contains("disabled", r.Error);
    }

    [Fact]
    public void Meta_actions_are_always_allowed()
    {
        var v = new PlanValidator(Array.Empty<string>());
        Assert.True(v.Validate(Plan.Json("clarify", new { question = "Which browser?" })).Ok);
        Assert.True(v.Validate(Plan.Json("unsupported", new { reason = "Can't send email." })).Ok);
    }

    [Fact]
    public void Rejects_missing_required_parameter() =>
        Assert.False(V().Validate(Plan.Json("open_application", new { })).Ok);

    [Fact]
    public void Rejects_unexpected_parameter() =>
        Assert.False(V().Validate(Plan.Json("open_application", new { application = "chrome", args = "--evil" })).Ok);

    [Fact]
    public void Rejects_unexpected_top_level_field() =>
        Assert.False(V().Validate("{\"action\":\"show_desktop\",\"parameters\":{},\"requires_confirmation\":false,\"explanation\":\"x\",\"command\":\"calc\"}").Ok);

    [Theory]
    [InlineData("{\"parameters\":{},\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"parameters\":{},\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"parameters\":{},\"requires_confirmation\":false}")]
    public void Rejects_missing_top_level_fields(string raw) => Assert.False(V().Validate(raw).Ok);

    [Theory]
    [InlineData("{\"action\":5,\"parameters\":{},\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"parameters\":[],\"requires_confirmation\":false,\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"parameters\":{},\"requires_confirmation\":\"no\",\"explanation\":\"x\"}")]
    [InlineData("{\"action\":\"show_desktop\",\"parameters\":{},\"requires_confirmation\":false,\"explanation\":7}")]
    public void Rejects_wrong_types(string raw) => Assert.False(V().Validate(raw).Ok);

    [Fact]
    public void Rejects_duplicate_fields() =>
        Assert.False(V().Validate("{\"action\":\"show_desktop\",\"action\":\"restore_windows\",\"parameters\":{},\"requires_confirmation\":false,\"explanation\":\"x\"}").Ok);

    [Theory]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("chrome; calc")]
    [InlineData("chrome && del *")]
    [InlineData("$(whoami)")]
    [InlineData("a|b")]
    [InlineData("\"quoted\"")]
    public void Rejects_names_with_path_or_shell_characters(string name) =>
        Assert.False(V().Validate(Plan.Json("open_application", new { application = name })).Ok);

    [Theory]
    [InlineData("Opera GX")]
    [InlineData("Visual Studio Code")]
    [InlineData("FrontAge V2")]
    [InlineData("7-Zip")]
    public void Accepts_plain_names(string name) =>
        Assert.True(V().Validate(Plan.Json("open_application", new { application = name })).Ok);

    [Fact]
    public void Strips_control_characters_from_explanation()
    {
        var r = V().Validate(Plan.Json("show_desktop", null, false, "Showing\u0007 desktop.\n\u001b[31m"));
        Assert.True(r.Ok);
        Assert.DoesNotContain('\u0007', r.Plan!.Explanation);
        Assert.DoesNotContain('\n', r.Plan.Explanation);
    }

    [Fact]
    public void Truncates_long_explanations()
    {
        var r = V().Validate(Plan.Json("show_desktop", null, false, new string('x', 5000)));
        Assert.True(r.Ok);
        Assert.Equal(PlanValidator.MaxTextLength, r.Plan!.Explanation.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    [InlineData(-5)]
    public void Rejects_ports_out_of_range(int port) =>
        Assert.False(V().Validate(Plan.Json("inspect_port", new { port })).Ok);

    [Fact]
    public void Accepts_port_as_number_or_digit_string()
    {
        Assert.Equal(3000, V().Validate(Plan.Json("inspect_port", new { port = 3000 })).Plan!.IntArg("port"));
        Assert.Equal(3000, V().Validate(Plan.Json("inspect_port", new { port = "3000" })).Plan!.IntArg("port"));
    }

    [Theory]
    [InlineData("3000.5")]
    [InlineData("abc")]
    [InlineData("-1")]
    public void Rejects_non_integer_ports(string port) =>
        Assert.False(V().Validate(Plan.Json("inspect_port", new { port })).Ok);

    [Fact]
    public void Enum_values_are_normalised_and_checked()
    {
        Assert.Equal("on", V().Validate(Plan.Json("bluetooth_set", new { state = " ON " })).Plan!.Arg("state"));
        Assert.Equal("windows_update", V().Validate(Plan.Json("open_settings", new { page = "Windows Update" })).Plan!.Arg("page"));
        Assert.False(V().Validate(Plan.Json("bluetooth_set", new { state = "toggle" })).Ok);
        Assert.False(V().Validate(Plan.Json("open_settings", new { page = "registry" })).Ok);
    }

    [Fact]
    public void Optional_parameter_may_be_omitted() =>
        Assert.True(V().Validate(Plan.Json("minimize_window", new { })).Ok);
}
