namespace FlowCommand.Core.Util;

public enum FocusKind { TextInput, NotTextInput, Unknown }

/// <summary>Facts gathered by the Windows layer about whatever currently has keyboard focus.</summary>
/// <param name="ControlType">UI Automation control type name, e.g. "Edit", "Document", "Pane".</param>
public sealed record FocusSignals(
    string ControlType, bool HasValuePattern, bool ValueReadOnly, bool HasTextPattern,
    bool IsWebContentProcess, bool HasCaret, bool IsShellSurface);

public sealed record FocusDecision(FocusKind Kind, string Reason);

/// <summary>
/// Decides whether a text box has focus from <see cref="FocusSignals"/>. Pure and unit-tested because the
/// UI Automation calls that feed it can only run on Windows.
/// </summary>
public static class FocusClassifier
{
    public static FocusDecision Classify(FocusSignals s)
    {
        if (s.IsShellSurface) return new(FocusKind.NotTextInput, "desktop or taskbar");
        if (s.HasCaret) return new(FocusKind.TextInput, "a text caret is showing");

        switch (s.ControlType)
        {
            case "Edit":
                return s.HasValuePattern && s.ValueReadOnly
                    ? new(FocusKind.NotTextInput, "read-only text")
                    : new(FocusKind.TextInput, "edit control");

            case "ComboBox":
                return s.HasValuePattern && !s.ValueReadOnly
                    ? new(FocusKind.TextInput, "editable combo box")
                    : new(FocusKind.NotTextInput, "combo box");

            case "Document":
                if (s.IsWebContentProcess) return new(FocusKind.NotTextInput, "web page body (no field focused)");
                if (s.HasValuePattern && s.ValueReadOnly) return new(FocusKind.NotTextInput, "read-only document");
                return s.HasTextPattern
                    ? new(FocusKind.TextInput, "text document")
                    : new(FocusKind.NotTextInput, "document without text input");

            default:
                if (s.HasTextPattern && s.HasValuePattern && !s.ValueReadOnly)
                    return new(FocusKind.TextInput, "editable text surface");
                // Browsers/Electron apps only build their accessibility tree after a client asks once.
                return s.IsWebContentProcess
                    ? new(FocusKind.Unknown, "web content; accessibility may not be active yet")
                    : new(FocusKind.NotTextInput, "not a text control");
        }
    }
}
