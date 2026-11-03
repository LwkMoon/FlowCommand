namespace FlowCommand.Core.Actions;

/// <param name="Success">The operation did what was asked (or, for dry runs, would have).</param>
/// <param name="Message">Short text shown to the user. Must not claim more than was verified.</param>
/// <param name="Verified">The outcome was checked against the system state after the call.</param>
/// <param name="KeepFocus">Do not pull focus back to the command palette (e.g. after activating another window).</param>
public sealed record ActionResult(bool Success, string Message, bool Verified = true, bool DryRun = false, bool KeepFocus = false)
{
    public static ActionResult Fail(string message) => new(false, message, false);
}

/// <summary>Context captured by the UI when the palette was opened.</summary>
/// <param name="PreviousForegroundWindow">The window that had focus when the palette opened ("minimise this").</param>
/// <param name="HideUi">Hides the palette (used before screenshots so it is not captured).</param>
public sealed record ActionContext(nint PreviousForegroundWindow, Action? HideUi = null);

public interface IActionHandler
{
    IReadOnlyCollection<string> Actions { get; }
    Task<ActionResult> ExecuteAsync(ValidatedPlan plan, ActionContext context, CancellationToken ct);
}

/// <summary>Optional: lets a handler describe exactly what will happen so the confirmation prompt is concrete.</summary>
public interface IConfirmationPreview
{
    Task<string?> PreviewAsync(ValidatedPlan plan, CancellationToken ct);
}
