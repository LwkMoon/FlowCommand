using System.Text.Json;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Pipeline;
using FlowCommand.Core.Safety;
using FlowCommand.Core.Util;
using Xunit;

namespace FlowCommand.Tests;

public class WebTargetsTests
{
    [Theory]
    [InlineData("https://github.com", "https://github.com/")]
    [InlineData("http://localhost:3000/app", "http://localhost:3000/app")]
    [InlineData("  https://www.bbc.com/news  ", "https://www.bbc.com/news")]
    public void Accepts_http_and_https(string input, string expected)
    {
        Assert.True(WebTargets.TryNormalizeWebsite(input, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("github.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ftp://example.com/file")]
    [InlineData("ms-settings:bluetooth")]
    [InlineData("https://user:password@example.com/")]
    public void Rejects_everything_else(string? input) => Assert.False(WebTargets.TryNormalizeWebsite(input, out _));

    [Fact]
    public void Rejects_very_long_urls() =>
        Assert.False(WebTargets.TryNormalizeWebsite("https://example.com/" + new string('a', 3000), out _));

    [Fact]
    public void Search_text_is_percent_encoded_into_the_template()
    {
        Assert.True(WebTargets.TryBuildSearchUrl(WebTargets.DefaultSearchUrl, "cats & dogs #1", out var url));
        Assert.Equal("https://www.google.com/search?q=cats%20%26%20dogs%20%231", url);
    }

    [Fact]
    public void Search_text_cannot_change_the_destination()
    {
        Assert.True(WebTargets.TryBuildSearchUrl("https://duckduckgo.com/?q={query}", "evil.com/@x?y=1&z=2", out var url));
        Assert.StartsWith("https://duckduckgo.com/?q=", url);
        Assert.Equal("duckduckgo.com", new Uri(url).Host);
        Assert.DoesNotContain("&z=2", url);
    }

    [Theory]
    [InlineData("http://www.google.com/search?q={query}")]      // not https
    [InlineData("https://www.google.com/search")]              // no placeholder
    [InlineData("https://x.test/?a={query}&b={query}")]        // two placeholders
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_bad_templates(string? template) => Assert.False(WebTargets.TryBuildSearchUrl(template, "hello", out _));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_queries(string? query) => Assert.False(WebTargets.TryBuildSearchUrl(WebTargets.DefaultSearchUrl, query, out _));
}

public class ProtectedProcessesTests
{
    [Theory]
    [InlineData("explorer")]
    [InlineData("Explorer.exe")]
    [InlineData("svchost")]
    [InlineData("winlogon")]
    [InlineData("Wispr Flow")]
    [InlineData("FlowCommand")]
    public void System_and_voice_input_processes_are_protected(string name) => Assert.True(ProtectedProcesses.IsProtected(name));

    [Theory]
    [InlineData("chrome")]
    [InlineData("Code")]
    [InlineData("notepad")]
    [InlineData("")]
    [InlineData(null)]
    public void Ordinary_apps_are_not(string? name) => Assert.False(ProtectedProcesses.IsProtected(name));
}

public class DefaultCatalogTests
{
    private static readonly Dictionary<string, ApplicationEntry> Apps = DefaultCatalog.Applications();

    [Fact]
    public void Ships_a_large_set_of_apps_and_sites()
    {
        Assert.True(Apps.Count >= 60, $"only {Apps.Count} apps");
        Assert.True(DefaultCatalog.Websites().Count >= 50);
    }

    [Fact]
    public void Every_app_is_well_formed()
    {
        foreach (var (id, a) in Apps)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.DisplayName), id);
            Assert.NotEmpty(a.Aliases);
            Assert.Matches("^[a-z0-9]+$", id);
            if (a.ProcessName != null)
            {
                Assert.False(a.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase), id + ": process names have no .exe");
                Assert.False(ProtectedProcesses.IsProtected(a.ProcessName), id + " must not map to a protected process");
            }
            foreach (var path in a.Paths)
            {
                Assert.StartsWith("%", path);
                Assert.DoesNotContain("..", path);
                Assert.DoesNotContain("\"", path);
            }
        }
    }

    [Fact]
    public void No_two_apps_answer_to_the_same_name()
    {
        var owners = new Dictionary<string, HashSet<string>>();
        foreach (var (id, a) in Apps)
            foreach (var name in new[] { id, a.DisplayName }.Concat(a.Aliases).Select(NameMatcher.Normalize).Where(n => n.Length > 0))
            {
                if (!owners.TryGetValue(name, out var set)) owners[name] = set = new HashSet<string>();
                set.Add(id);
            }
        var clashes = owners.Where(kv => kv.Value.Count > 1).Select(kv => $"{kv.Key}: {string.Join("/", kv.Value)}").ToList();
        Assert.True(clashes.Count == 0, "ambiguous names: " + string.Join("; ", clashes));
    }

    [Fact]
    public void Apps_sharing_a_process_name_are_told_apart_by_path()
    {
        var shared = Apps.Where(kv => kv.Value.ProcessName != null)
            .GroupBy(kv => kv.Value.ProcessName!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).ToList();
        foreach (var g in shared)
            Assert.All(g, kv => Assert.False(string.IsNullOrEmpty(kv.Value.PathContains), $"{kv.Key} shares process '{g.Key}' but has no pathContains"));
        Assert.Contains(shared, g => g.Key == "opera");
    }

    [Fact]
    public void Default_apps_point_at_real_entries()
    {
        foreach (var (category, id) in DefaultCatalog.DefaultApps())
            Assert.True(Apps.ContainsKey(id), $"defaultApps[{category}] -> {id} is missing");
    }

    [Fact]
    public void Every_website_is_a_valid_http_url_and_names_are_unique()
    {
        var sites = DefaultCatalog.Websites();
        foreach (var (name, url) in sites)
        {
            Assert.True(WebTargets.TryNormalizeWebsite(url, out var normalized), name);
            Assert.StartsWith("https://", normalized);
            Assert.Matches(@"^[a-z0-9 ]+$", name);
        }
        Assert.Equal(sites.Count, sites.Keys.Select(NameMatcher.Normalize).Distinct().Count());
    }

    [Fact]
    public void Terminals_are_launch_only()
    {
        // A console window belongs to a console host, not the shell, so these must not claim a process to close.
        Assert.Null(Apps["cmd"].ProcessName);
        Assert.Null(Apps["powershell"].ProcessName);
        Assert.Null(Apps["fileexplorer"].ProcessName);
    }
}

public class SettingsPageTests
{
    [Fact]
    public void Every_page_maps_to_a_distinct_settings_uri()
    {
        var uris = ActionCatalog.SettingsPageUris;
        Assert.True(uris.Count >= 30);
        Assert.Equal(uris.Count, uris.Values.Distinct().Count());
        foreach (var (page, uri) in uris)
        {
            Assert.Matches("^[a-z_]+$", page);
            Assert.StartsWith("ms-settings:", uri);
        }
        Assert.Equal(uris.Keys, ActionCatalog.SettingsPages);
        foreach (var original in new[] { "bluetooth", "wifi", "sound", "display", "apps", "notifications", "windows_update", "power", "personalization" })
            Assert.Contains(original, uris.Keys);
    }

    [Fact]
    public void Built_in_folder_names_are_unique_and_include_the_shell_locations()
    {
        var f = ActionCatalog.BuiltInFolders;
        Assert.Equal(f.Count, f.Distinct().Count());
        Assert.Contains("recycle_bin", f);
        Assert.Contains("this_pc", f);
        Assert.Contains("screenshots", f);
    }
}

public class NewActionTests
{
    private static PlanValidator V() => new(ActionCatalog.DefaultEnabled());

    [Fact]
    public void Media_control_accepts_commands_and_a_bounded_step_count()
    {
        Assert.True(V().Validate(Plan.Json("media_control", new { command = "play_pause" })).Ok);
        var up = V().Validate(Plan.Json("media_control", new { command = "volume_up", steps = 10 }));
        Assert.True(up.Ok, up.Error);
        Assert.Equal(10, up.Plan!.IntArg("steps"));
        Assert.Equal("play_pause", V().Validate(Plan.Json("media_control", new { command = "Play Pause" })).Plan!.Arg("command"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    [InlineData(-3)]
    public void Media_control_rejects_out_of_range_steps(int steps) =>
        Assert.False(V().Validate(Plan.Json("media_control", new { command = "volume_down", steps })).Ok);

    [Theory]
    [InlineData("reboot")]
    [InlineData("volume_set")]
    [InlineData("")]
    public void Media_control_rejects_unknown_commands(string command) =>
        Assert.False(V().Validate(Plan.Json("media_control", new { command })).Ok);

    [Fact]
    public void Screenshot_modes_are_checked()
    {
        Assert.True(V().Validate(Plan.Json("take_screenshot", new { mode = "full" })).Ok);
        Assert.True(V().Validate(Plan.Json("take_screenshot", new { mode = "region" })).Ok);
        Assert.False(V().Validate(Plan.Json("take_screenshot", new { mode = "window" })).Ok);
        Assert.False(V().Validate(Plan.Json("take_screenshot", new { })).Ok);
    }

    [Fact]
    public void Lock_takes_no_parameters()
    {
        Assert.True(V().Validate(Plan.Json("lock_workstation")).Ok);
        Assert.False(V().Validate(Plan.Json("lock_workstation", new { force = true })).Ok);
    }

    [Fact]
    public void Websites_are_looked_up_by_name_so_urls_are_rejected()
    {
        Assert.True(V().Validate(Plan.Json("open_website", new { site = "Google Docs" })).Ok);
        Assert.False(V().Validate(Plan.Json("open_website", new { site = "https://evil.example/login" })).Ok);
        Assert.False(V().Validate(Plan.Json("open_website", new { site = "file:///C:/Windows" })).Ok);
    }

    [Fact]
    public void Search_text_is_sanitised_and_bounded()
    {
        var r = V().Validate(Plan.Json("search_web", new { query = "best pizza\u0007 near me\n" }));
        Assert.True(r.Ok, r.Error);
        Assert.Equal("best pizza near me", r.Plan!.Arg("query"));
        Assert.False(V().Validate(Plan.Json("search_web", new { query = "   " })).Ok);
        Assert.Equal(PlanValidator.MaxTextLength, V().Validate(Plan.Json("search_web", new { query = new string('x', 900) })).Plan!.Arg("query").Length);
    }

    [Fact]
    public void Risk_classes_of_the_new_actions()
    {
        Assert.Equal(RiskLevel.Moderate, ActionCatalog.Find("lock_workstation")!.Risk);
        foreach (var name in new[] { "media_control", "take_screenshot", "open_website", "search_web" })
            Assert.Equal(RiskLevel.Low, ActionCatalog.Find(name)!.Risk);
    }

    [Fact]
    public void Actions_added_in_config_version_2_exist_and_are_enabled_by_default()
    {
        foreach (var name in ActionCatalog.AddedInConfigVersion2)
        {
            Assert.NotNull(ActionCatalog.Find(name));
            Assert.Contains(name, ActionCatalog.DefaultEnabled());
        }
    }

    [Fact]
    public async Task A_volume_command_reaches_its_handler_through_the_pipeline()
    {
        var cfg = Cfg.Default();
        var ai = new FakeAi().Reply(Plan.Json("media_control", new { command = "volume_up", steps = 8 }, false, "Turning the volume up."));
        var handler = new RecordingHandler();
        var pipe = new CommandPipeline(cfg, ai, new[] { handler });

        var i = await pipe.InterpretAsync("turn it up a lot", default);
        Assert.Equal(OutcomeKind.Ready, i.Kind);
        await pipe.ExecuteAsync(i, new ActionContext(0));
        Assert.Equal("media_control", handler.Executed.Single().Action);
    }
}

public class ConfigMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flowcmd-migrate-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "config.json");
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private AppConfig Load(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, json);
        return ConfigStore.Load(File_);
    }

    [Fact]
    public void A_config_from_an_older_release_gains_the_new_actions_and_the_default_websites()
    {
        var c = Load("{ \"enabledActions\": [\"open_application\", \"show_desktop\"] }");
        foreach (var name in ActionCatalog.AddedInConfigVersion2) Assert.Contains(name, c.EnabledActions);
        Assert.Contains("open_application", c.EnabledActions);
        Assert.DoesNotContain("kill_process_on_port", c.EnabledActions);   // nothing else is switched on
        Assert.Equal(AppConfig.CurrentVersion, c.ConfigVersion);
        Assert.True(c.Websites.Count >= 50);
    }

    [Fact]
    public void A_current_config_is_left_alone()
    {
        var c = Load("{ \"configVersion\": 2, \"enabledActions\": [\"show_desktop\"], \"websites\": {} }");
        Assert.Equal(new[] { "show_desktop" }, c.EnabledActions);
        Assert.Empty(c.Websites);
    }

    [Fact]
    public void An_invalid_search_url_is_replaced_by_the_default()
    {
        Assert.Equal(WebTargets.DefaultSearchUrl, Load("{ \"searchUrl\": \"http://insecure.test/?q={query}\" }").SearchUrl);
        Assert.Equal(WebTargets.DefaultSearchUrl, Load("{ \"searchUrl\": \"https://no-placeholder.test/\" }").SearchUrl);
        Assert.Equal("https://duckduckgo.com/?q={query}", Load("{ \"searchUrl\": \"https://duckduckgo.com/?q={query}\" }").SearchUrl);
    }

    [Fact]
    public void New_files_start_at_the_current_version()
    {
        Assert.Equal(AppConfig.CurrentVersion, AppConfig.CreateDefault().ConfigVersion);
        var created = ConfigStore.Load(File_);
        Assert.Equal(AppConfig.CurrentVersion, created.ConfigVersion);
        Assert.Contains("\"configVersion\": 2", File.ReadAllText(File_));
    }
}

public class ExampleConfigTests
{
    private static AppConfig Example() => ConfigStore.Load(Path.Combine(RepoPaths.Root(), "config.example.json"));

    [Fact]
    public void The_example_matches_the_built_in_defaults_so_the_two_cannot_drift()
    {
        var example = Example();
        var defaults = AppConfig.CreateDefault();

        Assert.Equal(defaults.Provider, example.Provider);
        Assert.Equal(defaults.Model, example.Model);
        Assert.Equal(defaults.RequestTimeoutSeconds, example.RequestTimeoutSeconds);
        Assert.Equal(defaults.SearchUrl, example.SearchUrl);
        Assert.Equal(defaults.DoubleTapKey, example.DoubleTapKey);
        Assert.Equal(defaults.EnabledActions.OrderBy(x => x), example.EnabledActions.OrderBy(x => x));
        Assert.Equal(defaults.DefaultApps.OrderBy(kv => kv.Key), example.DefaultApps.OrderBy(kv => kv.Key));
        Assert.Equal(defaults.Websites.OrderBy(kv => kv.Key), example.Websites.OrderBy(kv => kv.Key));
        Assert.Equal(defaults.Applications.Keys.OrderBy(x => x), example.Applications.Keys.OrderBy(x => x));
        foreach (var (id, app) in defaults.Applications)
            Assert.Equal(JsonSerializer.Serialize(app, ConfigStore.Options), JsonSerializer.Serialize(example.Applications[id], ConfigStore.Options));
    }

    [Fact]
    public void Example_projects_and_browser_setting_refer_to_real_apps()
    {
        var example = Example();
        Assert.NotEmpty(example.Projects);
        Assert.NotEmpty(example.Folders);
        foreach (var (name, project) in example.Projects)
            Assert.True(example.Applications.ContainsKey(project.Editor ?? "vscode"), $"project {name}: unknown editor");
        Assert.True(example.WebsiteBrowser == "" || example.Applications.ContainsKey(example.WebsiteBrowser));
    }

    [Fact]
    public void The_example_contains_no_secrets_or_personal_paths()
    {
        var text = File.ReadAllText(Path.Combine(RepoPaths.Root(), "config.example.json"));
        Assert.DoesNotContain("sk-", text);
        Assert.DoesNotContain("AIza", text);
        Assert.DoesNotContain(@"C:\\Users\\", text);
    }
}

public class PromptContentTests
{
    [Fact]
    public void The_prompt_lists_websites_and_explains_search()
    {
        var p = PromptBuilder.BuildSystemPrompt(Cfg.Default());
        Assert.Contains("Websites:", p);
        Assert.Contains("github", p);
        Assert.Contains("search_web", p);
        Assert.Contains("media_control", p);
        Assert.Contains("recycle_bin", p);
    }

    [Fact]
    public void App_lines_do_not_repeat_the_id_or_display_name_as_aliases()
    {
        var p = PromptBuilder.BuildSystemPrompt(Cfg.Default());
        Assert.DoesNotContain("aliases: chrome", p);
        Assert.Contains("aliases: code", p);
    }

    [Fact]
    public void The_prompt_stays_reasonably_small_with_the_full_catalog()
    {
        var p = PromptBuilder.BuildSystemPrompt(Cfg.Default());
        Assert.True(p.Length < 20000, $"system prompt is {p.Length} characters");
    }

    [Fact]
    public void Tool_parameters_cover_the_new_arguments()
    {
        var tool = PromptBuilder.BuildTool(Cfg.Default());
        foreach (var name in new[] { "command", "steps", "mode", "site", "query" })
            Assert.Contains(tool.Parameters, p => p.Name == name);
    }
}
