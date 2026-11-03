using FlowCommand.Core.Config;
using FlowCommand.Core.Util;
using Xunit;

namespace FlowCommand.Tests;

public class TriggerKeyTests
{
    [Theory]
    [InlineData("`", "backtick")]
    [InlineData("backtick", "backtick")]
    [InlineData("Grave", "backtick")]
    [InlineData(" Back Tick ", "backtick")]
    [InlineData("tilde", "backtick")]
    [InlineData("Caps Lock", "capslock")]
    [InlineData("CAPS", "capslock")]
    [InlineData("scroll-lock", "scrolllock")]
    [InlineData("Pause/Break", "pause")]
    public void Parses_names_and_aliases(string input, string id) => Assert.Equal(id, TriggerKeys.Parse(input)!.Id);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("F9")]
    [InlineData("enter")]
    [InlineData("a")]
    public void Unknown_keys_do_not_parse(string? input) => Assert.Null(TriggerKeys.Parse(input));

    [Fact]
    public void Default_is_the_backtick_key() => Assert.Equal("backtick", TriggerKeys.Default.Id);

    [Fact]
    public void Ids_are_unique_and_all_parse_back()
    {
        Assert.Equal(TriggerKeys.All.Count, TriggerKeys.All.Select(k => k.Id).Distinct().Count());
        foreach (var k in TriggerKeys.All) Assert.Same(k, TriggerKeys.Parse(k.Id));
    }

    [Fact]
    public void Backtick_is_matched_by_physical_position_not_by_layout()
    {
        var k = TriggerKeys.Backtick;
        Assert.True(k.Matches(vkCode: 0xC0, scanCode: 0x29, extended: false));   // US layout
        Assert.True(k.Matches(vkCode: 0xDF, scanCode: 0x29, extended: false));   // a layout that reports a different VK for the same key
        Assert.False(k.Matches(vkCode: 0xC0, scanCode: 0x27, extended: false));  // VK_OEM_3 on a key elsewhere (e.g. Ö on German)
        Assert.False(k.Matches(vkCode: 0xC0, scanCode: 0x29, extended: true));   // extended variants are different keys
        Assert.True(k.ProducesText);
    }

    [Fact]
    public void Lock_keys_are_matched_by_virtual_key()
    {
        Assert.True(TriggerKeys.CapsLock.Matches(0x14, 0x3A, false));
        Assert.False(TriggerKeys.CapsLock.Matches(0x91, 0x3A, false));
        Assert.True(TriggerKeys.ScrollLock.Matches(0x91, 0x46, false));
        Assert.True(TriggerKeys.Pause.Matches(0x13, 0x45, false));
        Assert.False(TriggerKeys.CapsLock.ProducesText);
    }
}

public class DoubleTapConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flowcmd-trigger-" + Guid.NewGuid().ToString("N"));
    private string Cfg_ => Path.Combine(_dir, "config.json");
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private AppConfig Load(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Cfg_, json);
        return ConfigStore.Load(Cfg_);
    }

    [Fact]
    public void An_old_config_with_the_caps_lock_flag_now_defaults_to_the_backtick_key()
    {
        var c = Load("{ \"doubleTapCapsLock\": true }");
        Assert.True(c.DoubleTapEnabled);
        Assert.Equal("backtick", c.DoubleTapKey);
        Assert.Null(c.LegacyDoubleTapCapsLock);
    }

    [Fact]
    public void An_old_config_that_disabled_the_trigger_stays_disabled()
    {
        var c = Load("{ \"doubleTapCapsLock\": false }");
        Assert.False(c.DoubleTapEnabled);
    }

    [Fact]
    public void Key_names_are_normalised_and_bad_values_fall_back_to_the_backtick()
    {
        Assert.Equal("capslock", Load("{ \"doubleTapKey\": \"Caps Lock\" }").DoubleTapKey);
        Assert.Equal("backtick", Load("{ \"doubleTapKey\": \"f9\" }").DoubleTapKey);
        Assert.Equal("backtick", Load("{ \"doubleTapKey\": null }").DoubleTapKey);
    }

    [Fact]
    public void Saving_does_not_write_the_legacy_property_back()
    {
        var c = Load("{ \"doubleTapCapsLock\": true }");
        ConfigStore.Save(Cfg_, c);
        var text = File.ReadAllText(Cfg_);
        Assert.DoesNotContain("doubleTapCapsLock", text);
        Assert.Contains("\"doubleTapKey\": \"backtick\"", text);
        Assert.Contains("\"doubleTapEnabled\": true", text);
    }

    [Fact]
    public void Trigger_description_names_the_chosen_key()
    {
        var c = AppConfig.CreateDefault();
        c.DoubleTapKey = "capslock";
        Assert.Equal("double-tap Caps Lock", c.TriggerDescription());
        c.DoubleTapKey = "pause";
        Assert.Equal("double-tap Pause", c.TriggerDescription());
    }
}
