using System.Text.Json;
using FlowCommand.Core.Ai;
using Xunit;

namespace FlowCommand.Tests;

public class ToolSchemaTests
{
    private static JsonDocument Gemini(Action<FlowCommand.Core.Config.AppConfig>? tweak = null)
    {
        var cfg = Cfg.Default();
        tweak?.Invoke(cfg);
        return JsonDocument.Parse(JsonSerializer.Serialize(ToolSchemas.ForGemini(PromptBuilder.BuildTool(cfg))));
    }

    private static IEnumerable<JsonElement> Walk(JsonElement e)
    {
        yield return e;
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) foreach (var c in Walk(p.Value)) yield return c;
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var i in e.EnumerateArray()) foreach (var c in Walk(i)) yield return c;
    }

    [Fact]
    public void Gemini_schema_uses_only_the_supported_subset()
    {
        using var doc = Gemini();
        var json = doc.RootElement.GetRawText();
        Assert.DoesNotContain("additionalProperties", json);
        Assert.DoesNotContain("\"$schema\"", json);
        Assert.DoesNotContain("input_schema", json);
    }

    [Fact]
    public void Every_object_in_the_gemini_schema_has_non_empty_properties()
    {
        using var doc = Gemini();
        var objects = Walk(doc.RootElement)
            .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "object")
            .ToList();
        Assert.True(objects.Count >= 2);
        foreach (var o in objects)
        {
            Assert.True(o.TryGetProperty("properties", out var props), "object without properties");
            Assert.True(props.EnumerateObject().Any(), "object with empty properties");
        }
    }

    [Fact]
    public void Gemini_requires_three_fields_and_leaves_parameters_optional()
    {
        using var doc = Gemini();
        var required = doc.RootElement.GetProperty("parameters").GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(new[] { "action", "requires_confirmation", "explanation" }, required);
    }

    [Fact]
    public void Parameter_properties_describe_every_argument_with_the_right_types()
    {
        using var doc = Gemini();
        var props = doc.RootElement.GetProperty("parameters").GetProperty("properties").GetProperty("parameters").GetProperty("properties");
        foreach (var name in new[] { "application", "folder", "project", "page", "state", "port", "question", "reason" })
            Assert.True(props.TryGetProperty(name, out _), "missing " + name);

        Assert.Equal("integer", props.GetProperty("port").GetProperty("type").GetString());
        Assert.Equal("string", props.GetProperty("application").GetProperty("type").GetString());
        Assert.Equal(new[] { "on", "off" }, props.GetProperty("state").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public void Disabled_actions_leave_the_action_enum_for_both_providers()
    {
        var cfg = Cfg.Default();
        cfg.EnabledActions.Remove("kill_process_on_port");
        var tool = PromptBuilder.BuildTool(cfg);
        Assert.DoesNotContain("kill_process_on_port", tool.Actions);
        Assert.Contains("clarify", tool.Actions);
        Assert.DoesNotContain("kill_process_on_port", JsonSerializer.Serialize(ToolSchemas.ForGemini(tool)));
        Assert.DoesNotContain("kill_process_on_port", JsonSerializer.Serialize(ToolSchemas.ForAnthropic(tool)));
    }

    [Fact]
    public void Anthropic_schema_is_still_strict_at_the_top_level()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ToolSchemas.ForAnthropic(PromptBuilder.BuildTool(Cfg.Default()))));
        var schema = doc.RootElement.GetProperty("input_schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(4, schema.GetProperty("required").GetArrayLength());
    }
}

public class ProviderCatalogTests
{
    [Theory]
    [InlineData(null, "gemini")]
    [InlineData("", "gemini")]
    [InlineData("  GEMINI ", "gemini")]
    [InlineData("Anthropic", "anthropic")]
    public void Normalizes_names(string? input, string expected) => Assert.Equal(expected, ProviderCatalog.Normalize(input));

    [Fact]
    public void Default_models_and_env_vars()
    {
        Assert.Equal("gemini-3.5-flash-lite", ProviderCatalog.DefaultModel("gemini"));
        Assert.Equal("claude-haiku-5-5", ProviderCatalog.DefaultModel("anthropic"));
        Assert.Contains("GEMINI_API_KEY", ProviderCatalog.EnvVars("gemini"));
        Assert.Contains("ANTHROPIC_API_KEY", ProviderCatalog.EnvVars("anthropic"));
        Assert.Empty(ProviderCatalog.EnvVars("mystery"));
    }

    [Fact]
    public void Retired_gemini_models_are_not_the_default()
    {
        Assert.DoesNotContain("2.5", ProviderCatalog.DefaultModel("gemini"));
    }
}
