using System.Globalization;

namespace FlowCommand.Core.Actions;

/// <summary>A plan that has passed schema validation. Contains only known fields with sanitised values.</summary>
public sealed class ValidatedPlan
{
    public ValidatedPlan(ActionSpec spec, IReadOnlyDictionary<string, string> args,
        bool modelRequestedConfirmation, string explanation)
    {
        Spec = spec;
        Args = args;
        ModelRequestedConfirmation = modelRequestedConfirmation;
        Explanation = explanation;
    }

    public ActionSpec Spec { get; }
    public string Action => Spec.Name;
    public IReadOnlyDictionary<string, string> Args { get; }
    public bool ModelRequestedConfirmation { get; }
    public string Explanation { get; }

    public string Arg(string name) => Args[name];
    public string? ArgOrNull(string name) => Args.TryGetValue(name, out var v) ? v : null;
    public int IntArg(string name) => int.Parse(Args[name], CultureInfo.InvariantCulture);

    public string Describe() =>
        $"{Action}({string.Join(", ", Args.Select(kv => $"{kv.Key}={kv.Value}"))})";
}

public sealed record ValidationResult(ValidatedPlan? Plan, string? Error)
{
    public bool Ok => Plan != null;
}
