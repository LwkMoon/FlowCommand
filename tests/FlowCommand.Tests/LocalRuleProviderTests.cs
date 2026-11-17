using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Pipeline;
using Xunit;

namespace FlowCommand.Tests;

/// <summary>The offline interpreter must only ever produce plans the strict validator accepts.</summary>
public class LocalRuleProviderTests
{
    private static AppConfig Config()
    {
        var c = AppConfig.CreateDefault();
        c.Folders["projects"] = @"C:\dev";
        c.Projects["frontage"] = new ProjectEntry { Path = @"C:\dev\frontage", Aliases = { "front age" } };
        return c;
    }

    private static ValidatedPlan Run(string text, AppConfig? cfg = null)
    {
        cfg ??= Config();
        var raw = new LocalRuleProvider(cfg).Interpret(text);
        var result = new PlanValidator(cfg.EnabledActions).Validate(raw);
        Assert.True(result.Ok, $"'{text}' produced an invalid plan: {result.Error} / {raw}");
        return result.Plan!;
    }

    [Theory]
    [InlineData("open chrome", "open_application", "application", "chrome")]
    [InlineData("Okay, open Google Chrome please.", "open_application", "application", "chrome")]
    [InlineData("launch vs code", "open_application", "application", "vscode")]
    [InlineData("open my browser", "open_application", "application", "chrome")]
    [InlineData("open the calculator app", "open_application", "application", "calculator")]
    [InlineData("open spotify", "open_application", "application", "spotify")]
    [InlineData("open youtube", "open_website", "site", "youtube")]
    [InlineData("go to github", "open_website", "site", "github")]
    [InlineData("open downloads", "open_folder", "folder", "downloads")]
    [InlineData("open my documents folder", "open_folder", "folder", "documents")]
    [InlineData("open recycle bin", "open_folder", "folder", "recycle_bin")]
    [InlineData("open projects", "open_folder", "folder", "projects")]
    [InlineData("open project frontage", "open_project", "project", "frontage")]
    [InlineData("open bluetooth settings", "open_settings", "page", "bluetooth")]
    [InlineData("settings wi-fi", "open_settings", "page", "wifi")]
    [InlineData("open windows update settings", "open_settings", "page", "windows_update")]
    [InlineData("close chrome", "close_application", "application", "chrome")]
    [InlineData("quit the discord app", "close_application", "application", "discord")]
    [InlineData("switch to firefox", "activate_window", "application", "firefox")]
    [InlineData("minimize chrome", "minimize_window", "application", "chrome")]
    [InlineData("maximize vs code", "maximize_window", "application", "vscode")]
    [InlineData("search for cheap flights", "search_web", "query", "cheap flights")]
    [InlineData("google cats", "search_web", "query", "cats")]
    [InlineData("turn bluetooth off", "bluetooth_set", "state", "off")]
    [InlineData("bluetooth on", "bluetooth_set", "state", "on")]
    [InlineData("what is using port 3000", "inspect_port", "port", "3000")]
    [InlineData("take a screenshot", "take_screenshot", "mode", "full")]
    [InlineData("snip", "take_screenshot", "mode", "region")]
    [InlineData("pause", "media_control", "command", "play_pause")]
    [InlineData("next song", "media_control", "command", "next_track")]
    [InlineData("previous track", "media_control", "command", "previous_track")]
    [InlineData("mute", "media_control", "command", "mute")]
    [InlineData("volume up", "media_control", "command", "volume_up")]
    [InlineData("turn it down", "media_control", "command", "volume_down")]
    [InlineData("make it louder", "media_control", "command", "volume_up")]
    public void Understands_common_commands(string text, string action, string arg, string value)
    {
        var plan = Run(text);
        Assert.Equal(action, plan.Action);
        Assert.Equal(value, plan.Arg(arg));
    }

    [Theory]
    [InlineData("show desktop", "show_desktop")]
    [InlineData("restore windows", "restore_windows")]
    [InlineData("lock my pc", "lock_workstation")]
    [InlineData("lock the computer", "lock_workstation")]
    [InlineData("minimize", "minimize_window")]
    [InlineData("maximize this window", "maximize_window")]
    public void Understands_commands_without_arguments(string text, string action) =>
        Assert.Equal(action, Run(text).Action);

    [Fact]
    public void Volume_steps_follow_the_wording()
    {
        Assert.Equal("12", Run("turn it up a lot").Arg("steps"));
        Assert.Equal("2", Run("turn the volume down a little").Arg("steps"));
        Assert.Null(Run("volume up").ArgOrNull("steps"));
    }

    [Fact]
    public void Killing_a_process_always_asks_for_confirmation()
    {
        var plan = Run("kill the process on port 8080");
        Assert.Equal("kill_process_on_port", plan.Action);
        Assert.Equal("8080", plan.Arg("port"));
        Assert.True(plan.ModelRequestedConfirmation);
    }

    [Theory]
    [InlineData("turn up the brightness")]          // looks like volume, is not
    [InlineData("what is the weather like in lahore")]
    [InlineData("write me a poem")]
    [InlineData("open chrome and then youtube")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unknown_requests_are_reported_as_unsupported_and_never_guessed(string text)
    {
        var cfg = Config();
        var raw = new LocalRuleProvider(cfg).Interpret(text);
        var result = new PlanValidator(cfg.EnabledActions).Validate(raw);
        Assert.True(result.Ok);
        Assert.Equal(ActionCatalog.Unsupported, result.Plan!.Action);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("close")]
    [InlineData("search")]
    [InlineData("open settings")]
    public void Incomplete_requests_ask_a_question(string text) =>
        Assert.Equal(ActionCatalog.Clarify, Run(text).Action);

    [Fact]
    public void Ambiguous_app_names_ask_which_one()
    {
        var cfg = Config();
        cfg.Applications["adobereader"] = new ApplicationEntry { DisplayName = "Adobe Reader", Aliases = { "adobe reader" } };
        cfg.Applications["adobeacrobat"] = new ApplicationEntry { DisplayName = "Adobe Acrobat Pro", Aliases = { "adobe acrobat pro" } };
        var plan = Run("open adobe", cfg);
        Assert.Equal(ActionCatalog.Clarify, plan.Action);
        Assert.Contains("Adobe", plan.Arg("question"));
    }

    [Fact]
    public void Apps_that_are_not_configured_are_passed_on_by_name()
    {
        var plan = Run("open obsidian vault tool");
        Assert.Equal("open_application", plan.Action);
        Assert.Equal("obsidian vault tool", plan.Arg("application"));
    }

    [Fact]
    public void Disabled_actions_are_still_blocked_by_the_validator()
    {
        var cfg = Config();
        cfg.EnabledActions.Remove("lock_workstation");
        var raw = new LocalRuleProvider(cfg).Interpret("lock my pc");
        var result = new PlanValidator(cfg.EnabledActions).Validate(raw);
        Assert.False(result.Ok);
        Assert.Contains("disabled", result.Error!);
    }

    [Fact]
    public void Text_cannot_inject_extra_fields_or_actions()
    {
        // the whole phrase becomes one app name or is refused; it can never smuggle in another action
        var cfg = Config();
        var raw = new LocalRuleProvider(cfg).Interpret("open notepad\", \"action\": \"kill_process_on_port");
        var result = new PlanValidator(cfg.EnabledActions).Validate(raw);
        if (result.Ok) Assert.NotEqual("kill_process_on_port", result.Plan!.Action);
    }

    [Fact]
    public void Works_through_the_whole_pipeline_with_no_network_and_no_key()
    {
        var cfg = Config();
        var handler = new RecordingHandler();
        var pipeline = new CommandPipeline(cfg, new LocalRuleProvider(cfg), new[] { handler });
        var outcome = pipeline.InterpretAsync("Open Chrome.", CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(OutcomeKind.Ready, outcome.Kind);
        Assert.Equal("open_application", outcome.Plan!.Action);
    }

    [Fact]
    public void The_factory_builds_the_offline_provider_for_the_default_config()
    {
        var provider = AiProviderFactory.Create(AppConfig.CreateDefault(), new HttpClient(), () => null);
        Assert.Equal("local", provider.Name);
    }

    [Fact]
    public void The_offline_provider_needs_no_api_key()
    {
        Assert.False(ProviderCatalog.UsesApiKey("local"));
        Assert.True(ProviderCatalog.UsesApiKey("gemini"));
        Assert.True(ProviderCatalog.UsesApiKey("anthropic"));
    }
}

public class FirstRunConfigTests
{
    [Fact]
    public void A_brand_new_config_still_needs_the_welcome_screen()
    {
        var created = AppConfig.CreateDefault();
        Assert.False(created.FirstRunCompleted);
        Assert.Equal(AppConfig.CurrentVersion, created.ConfigVersion);
    }

    [Fact]
    public void Configs_written_by_older_releases_skip_the_welcome_screen()
    {
        var old = new AppConfig { ConfigVersion = 2, Provider = "gemini" };
        ConfigStore.Normalize(old);
        Assert.True(old.FirstRunCompleted);
        Assert.Equal("gemini", old.Provider);             // an existing choice is never changed
    }

    [Fact]
    public void The_flag_survives_a_save_and_reload()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flowcmd-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var file = Path.Combine(dir, "config.json");
            var cfg = ConfigStore.Load(file);
            Assert.False(cfg.FirstRunCompleted);
            cfg.FirstRunCompleted = true;
            ConfigStore.Save(file, cfg);
            Assert.True(ConfigStore.Load(file).FirstRunCompleted);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
