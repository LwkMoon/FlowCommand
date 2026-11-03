using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlowCommand.Core.Actions;

/// <summary>
/// Strict validation of the model's structured output. Rejects malformed JSON, unknown/duplicate fields,
/// unknown or disabled actions, missing/unexpected/ill-typed parameters.
/// </summary>
public sealed class PlanValidator
{
    private static readonly Regex NamePattern = new(@"^[\p{L}\p{N} ._\-+#'()]{1,80}$", RegexOptions.Compiled);
    public const int MaxTextLength = 300;

    private readonly HashSet<string> _enabled;

    public PlanValidator(IEnumerable<string> enabledActions) =>
        _enabled = new HashSet<string>(enabledActions, StringComparer.Ordinal);

    private static ValidationResult Fail(string why) => new(null, why);

    public ValidationResult Validate(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return Fail("empty response");
        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { return Fail("response was not valid JSON"); }
        using (doc) return ValidateRoot(doc.RootElement);
    }

    private ValidationResult ValidateRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return Fail("response was not a JSON object");

        string? action = null;
        JsonElement? parameters = null;
        bool? confirm = null;
        string? explanation = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var p in root.EnumerateObject())
        {
            if (!seen.Add(p.Name)) return Fail($"duplicate field '{p.Name}'");
            switch (p.Name)
            {
                case "action":
                    if (p.Value.ValueKind != JsonValueKind.String) return Fail("'action' must be a string");
                    action = p.Value.GetString();
                    break;
                case "parameters":
                    if (p.Value.ValueKind != JsonValueKind.Object) return Fail("'parameters' must be an object");
                    parameters = p.Value;
                    break;
                case "requires_confirmation":
                    if (p.Value.ValueKind == JsonValueKind.True) confirm = true;
                    else if (p.Value.ValueKind == JsonValueKind.False) confirm = false;
                    else return Fail("'requires_confirmation' must be true or false");
                    break;
                case "explanation":
                    if (p.Value.ValueKind != JsonValueKind.String) return Fail("'explanation' must be a string");
                    explanation = p.Value.GetString();
                    break;
                default:
                    return Fail($"unexpected field '{Sanitize(p.Name, 40)}'");
            }
        }

        if (action == null) return Fail("missing 'action'");
        if (parameters == null) return Fail("missing 'parameters'");
        if (confirm == null) return Fail("missing 'requires_confirmation'");
        if (explanation == null) return Fail("missing 'explanation'");

        var spec = ActionCatalog.Find(action);
        if (spec == null) return Fail($"unknown action '{Sanitize(action, 40)}'");
        if (!spec.Meta && !_enabled.Contains(spec.Name)) return Fail($"action '{spec.Name}' is disabled in settings");

        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in parameters.Value.EnumerateObject())
        {
            var ps = spec.Parameters.FirstOrDefault(x => x.Name == p.Name);
            if (ps == null) return Fail($"unexpected parameter '{Sanitize(p.Name, 40)}' for {spec.Name}");
            if (args.ContainsKey(p.Name)) return Fail($"duplicate parameter '{p.Name}'");
            var (ok, value, err) = ValidateParam(ps, p.Value);
            if (!ok) return Fail(err!);
            args[p.Name] = value!;
        }
        foreach (var ps in spec.Parameters.Where(x => x.Required))
            if (!args.ContainsKey(ps.Name)) return Fail($"missing required parameter '{ps.Name}'");

        var expl = Sanitize(explanation, MaxTextLength);
        return new ValidationResult(new ValidatedPlan(spec, args, confirm.Value, expl), null);
    }

    private static (bool Ok, string? Value, string? Error) ValidateParam(ParamSpec ps, JsonElement e)
    {
        switch (ps.Kind)
        {
            case ParamKind.Name:
            {
                if (e.ValueKind != JsonValueKind.String) return (false, null, $"'{ps.Name}' must be a string");
                var s = Sanitize(e.GetString() ?? "", 200);
                if (!NamePattern.IsMatch(s)) return (false, null, $"'{ps.Name}' contains characters that are not allowed");
                return (true, s, null);
            }
            case ParamKind.Text:
            {
                if (e.ValueKind != JsonValueKind.String) return (false, null, $"'{ps.Name}' must be a string");
                var s = Sanitize(e.GetString() ?? "", MaxTextLength);
                if (s.Length == 0) return (false, null, $"'{ps.Name}' is empty");
                return (true, s, null);
            }
            case ParamKind.Integer:
            {
                int n;
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out n)) { }
                else if (e.ValueKind == JsonValueKind.String &&
                         int.TryParse(e.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out n)) { }
                else return (false, null, $"'{ps.Name}' must be a whole number");
                if (n < ps.Min || n > ps.Max) return (false, null, $"'{ps.Name}' must be between {ps.Min} and {ps.Max}");
                return (true, n.ToString(CultureInfo.InvariantCulture), null);
            }
            case ParamKind.Enum:
            {
                if (e.ValueKind != JsonValueKind.String) return (false, null, $"'{ps.Name}' must be a string");
                var v = Sanitize(e.GetString() ?? "", 60).ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
                if (ps.AllowedValues == null || !ps.AllowedValues.Contains(v))
                    return (false, null, $"'{ps.Name}' must be one of: {string.Join(", ", ps.AllowedValues ?? Array.Empty<string>())}");
                return (true, v, null);
            }
            default:
                return (false, null, "unsupported parameter kind");
        }
    }

    /// <summary>Removes control characters, trims and truncates.</summary>
    public static string Sanitize(string s, int max)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) if (!char.IsControl(ch)) sb.Append(ch);
        var t = sb.ToString().Trim();
        return t.Length > max ? t[..max] : t;
    }
}
