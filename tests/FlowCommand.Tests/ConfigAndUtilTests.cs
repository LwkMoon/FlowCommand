using FlowCommand.Core.Actions;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;
using Xunit;

namespace FlowCommand.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flowcmd-tests-" + Guid.NewGuid().ToString("N"));
    private string File_ => Path.Combine(_dir, "config.json");
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void Missing_file_creates_defaults()
    {
        var cfg = ConfigStore.Load(File_);
        Assert.True(File.Exists(File_));
        Assert.Equal("gemini", cfg.Provider);
        Assert.Equal("gemini-3.5-flash-lite", cfg.Model);
        Assert.Contains("chrome", cfg.Applications.Keys);
        Assert.Equal(ActionCatalog.DefaultEnabled().Count, cfg.EnabledActions.Count);
    }

    [Fact]
    public void Round_trips_user_changes()
    {
        var cfg = ConfigStore.Load(File_);
        cfg.Model = "some-other-model";
        cfg.Projects["frontage"] = new ProjectEntry { Path = @"C:\dev\frontage-v2", Aliases = { "frontage v2" } };
        ConfigStore.Save(File_, cfg);

        var again = ConfigStore.Load(File_);
        Assert.Equal("some-other-model", again.Model);
        Assert.Equal(@"C:\dev\frontage-v2", again.Projects["frontage"].Path);
    }

    [Fact]
    public void Invalid_json_throws_a_config_exception()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "{ nope");
        Assert.Throws<ConfigException>(() => ConfigStore.Load(File_));
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "{ // hello\n \"model\": \"m\", }");
        Assert.Equal("m", ConfigStore.Load(File_).Model);
    }

    [Fact]
    public void Values_are_clamped_and_nulls_repaired()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(File_, "{\"configVersion\":2,\"autoSubmitDelayMs\":5,\"autoSubmitMode\":\"weird\",\"applications\":null,\"enabledActions\":[\"show_desktop\",\"rm_rf\",\"clarify\"]}");
        var c = ConfigStore.Load(File_);
        Assert.Equal(200, c.AutoSubmitDelayMs);
        Assert.Equal("paste", c.AutoSubmitMode);
        Assert.NotNull(c.Applications);
        Assert.Equal(new[] { "show_desktop" }, c.EnabledActions);   // unknown + meta names dropped
    }

    [Fact]
    public void Config_model_has_no_secret_fields()
    {
        var names = typeof(AppConfig).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain(names, n => n.Contains("apikey") || n.Contains("secret") || n.Contains("token") || n.Contains("password"));
        var json = File.Exists(File_) ? File.ReadAllText(File_) : System.Text.Json.JsonSerializer.Serialize(AppConfig.CreateDefault(), ConfigStore.Options);
        Assert.DoesNotContain("sk-", json);
    }
}

public class HotkeyParserTests
{
    [Fact]
    public void Parses_modifiers_and_key()
    {
        var r = HotkeyParser.Parse("ctrl + alt + space");
        Assert.True(r.Ok);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, r.Hotkey!.Modifiers);
        Assert.Equal(0x20u, r.Hotkey.VirtualKey);
        Assert.Equal("Ctrl+Alt+space", r.Hotkey.Display.Replace("Space", "space"));
    }

    [Theory]
    [InlineData("Ctrl+Shift+K", 0x4Bu)]
    [InlineData("Win+Alt+1", 0x31u)]
    [InlineData("F13", 0x7Cu)]
    [InlineData("Ctrl+Alt+`", 0xC0u)]
    public void Parses_assorted_keys(string text, uint vk)
    {
        var r = HotkeyParser.Parse(text);
        Assert.True(r.Ok, r.Error);
        Assert.Equal(vk, r.Hotkey!.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Space")]            // no modifier
    [InlineData("A")]                // no modifier
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Hyper+K")]
    [InlineData("Ctrl++K")]
    public void Rejects_bad_hotkeys(string text) => Assert.False(HotkeyParser.Parse(text).Ok);
}

public class NameMatcherTests
{
    private static IReadOnlyList<IReadOnlyList<string>> Names(params string[][] n) => n.Select(x => (IReadOnlyList<string>)x).ToList();

    [Theory]
    [InlineData("Opera GX", 1)]
    [InlineData("operagx", 1)]
    [InlineData("opera gx browser", 1)]
    [InlineData("Google Chrome", 0)]
    [InlineData("chrome", 0)]
    public void Finds_forgivingly(string query, int expected)
    {
        var names = Names(new[] { "chrome", "Google Chrome" }, new[] { "operagx", "Opera GX", "gx" });
        var m = NameMatcher.Find(query, names);
        Assert.True(m.Found);
        Assert.Equal(expected, m.Index);
    }

    [Fact]
    public void Reports_ambiguity()
    {
        var m = NameMatcher.Find("visual studio", Names(new[] { "Visual Studio Code" }, new[] { "Visual Studio 2022" }));
        Assert.True(m.IsAmbiguous);
        Assert.False(m.Found);
        Assert.Equal(2, m.Ambiguous.Count);
    }

    [Fact]
    public void Exact_beats_partial()
    {
        var m = NameMatcher.Find("Visual Studio Code", Names(new[] { "Visual Studio Code" }, new[] { "Visual Studio Code - Insiders" }));
        Assert.True(m.Found);
        Assert.Equal(0, m.Index);
    }

    [Fact]
    public void Unknown_or_empty_names_do_not_match()
    {
        Assert.False(NameMatcher.Find("spotify", Names(new[] { "chrome" })).Found);
        Assert.False(NameMatcher.Find("", Names(new[] { "chrome" })).Found);
        Assert.False(NameMatcher.Find("ab", Names(new[] { "abc def" })).Found);   // too short for partial matching
    }

    [Fact]
    public void Plus_and_hash_are_significant()
    {
        var names = Names(new[] { "notepad" }, new[] { "notepad++", "notepad plus plus" }, new[] { "c" }, new[] { "c#" });
        Assert.Equal(0, NameMatcher.Find("notepad", names).Index);
        Assert.Equal(1, NameMatcher.Find("Notepad++", names).Index);
        Assert.Equal(1, NameMatcher.Find("notepad plus plus", names).Index);
        Assert.Equal(3, NameMatcher.Find("C#", names).Index);
        Assert.Equal(2, NameMatcher.Find("c", names).Index);
    }
}

public class TriggerConfigTests
{
    [Fact]
    public void Defaults_follow_the_requested_behaviour()
    {
        var c = AppConfig.CreateDefault();
        Assert.Equal("gemini", c.Provider);
        Assert.True(c.DoubleTapEnabled);
        Assert.Equal("backtick", c.DoubleTapKey);
        Assert.True(c.IgnoreWhenTextBoxFocused);
        Assert.Equal("", c.Hotkey);                       // the extra hotkey is optional and off by default
        Assert.Contains("chrome", c.WebContentProcesses);
        Assert.Equal("double-tap ` (backtick)", c.TriggerDescription());
    }

    [Fact]
    public void Trigger_description_covers_all_combinations()
    {
        var c = AppConfig.CreateDefault();
        c.Hotkey = "Ctrl+Alt+Space";
        Assert.Equal("double-tap ` (backtick) or Ctrl+Alt+Space", c.TriggerDescription());
        c.DoubleTapEnabled = false;
        Assert.Equal("Ctrl+Alt+Space", c.TriggerDescription());
        c.Hotkey = "";
        Assert.Contains("no trigger", c.TriggerDescription());
    }

    [Fact]
    public void Normalize_repairs_new_fields()
    {
        var c = AppConfig.CreateDefault();
        c.DoubleTapIntervalMs = 5;
        c.GeminiThinkingLevel = "ultra";
        c.Provider = "  Anthropic ";
        c.Model = "";
        ConfigStore.Normalize(c);
        Assert.Equal(150, c.DoubleTapIntervalMs);
        Assert.Equal("low", c.GeminiThinkingLevel);
        Assert.Equal("anthropic", c.Provider);
        Assert.Equal("claude-haiku-5-5", c.Model);
    }

    [Fact]
    public void Clone_is_independent()
    {
        var a = AppConfig.CreateDefault();
        var b = a.Clone();
        b.Model = "other";
        b.EnabledActions.Clear();
        Assert.Equal("gemini-3.5-flash-lite", a.Model);
        Assert.NotEmpty(a.EnabledActions);
    }
}
