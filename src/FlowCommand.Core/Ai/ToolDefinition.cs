using FlowCommand.Core.Actions;

namespace FlowCommand.Core.Ai;

/// <summary>
/// Provider-neutral description of the single "submit_command" tool. Each provider turns this into
/// its own wire format via <see cref="ToolSchemas"/>.
/// </summary>
public sealed record ToolDefinition(
    string Name, string Description, IReadOnlyList<string> Actions, IReadOnlyList<ParamSpec> Parameters);

public static class ToolSchemas
{
    private static Dictionary<string, object> ParameterProperties(ToolDefinition t)
    {
        var props = new Dictionary<string, object>();
        foreach (var p in t.Parameters)
        {
            var schema = new Dictionary<string, object>();
            switch (p.Kind)
            {
                case ParamKind.Integer:
                    schema["type"] = "integer";
                    schema["description"] = $"{p.Description} ({p.Min}-{p.Max})";
                    break;
                case ParamKind.Enum:
                    schema["type"] = "string";
                    schema["enum"] = (p.AllowedValues ?? Array.Empty<string>()).ToArray();
                    schema["description"] = p.Description;
                    break;
                default:
                    schema["type"] = "string";
                    schema["description"] = p.Description;
                    break;
            }
            props[p.Name] = schema;
        }
        return props;
    }

    private static Dictionary<string, object> CoreProperties(ToolDefinition t)
    {
        return new Dictionary<string, object>
        {
            ["action"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = t.Actions.ToArray(),
                ["description"] = "Which action to perform.",
            },
            ["parameters"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = "Arguments for the chosen action, exactly as listed in the instructions. Use {} when it has none.",
                ["properties"] = ParameterProperties(t),
            },
            ["requires_confirmation"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "true for anything destructive or when unsure.",
            },
            ["explanation"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "One short present-tense sentence shown to the user.",
            },
        };
    }

    /// <summary>Anthropic tool: full JSON Schema, strict top level.</summary>
    public static Dictionary<string, object> ForAnthropic(ToolDefinition t) => new()
    {
        ["name"] = t.Name,
        ["description"] = t.Description,
        ["input_schema"] = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = CoreProperties(t),
            ["required"] = new[] { "action", "parameters", "requires_confirmation", "explanation" },
            ["additionalProperties"] = false,
        },
    };

    /// <summary>
    /// Gemini function declaration. Gemini accepts only a subset of OpenAPI (type/properties/enum/required/description),
    /// so: no additionalProperties, and every object has non-empty properties. "parameters" is not marked required
    /// because Gemini may omit an empty object; <see cref="GeminiProvider"/> restores it as {}.
    /// </summary>
    public static Dictionary<string, object> ForGemini(ToolDefinition t) => new()
    {
        ["name"] = t.Name,
        ["description"] = t.Description,
        ["parameters"] = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = CoreProperties(t),
            ["required"] = new[] { "action", "requires_confirmation", "explanation" },
        },
    };
}
