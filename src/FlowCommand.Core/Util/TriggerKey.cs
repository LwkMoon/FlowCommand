namespace FlowCommand.Core.Util;

/// <summary>A key that can be double-tapped to open the palette.</summary>
/// <param name="Id">Name used in config.json.</param>
/// <param name="ScanCode">If non-zero the key is matched by its physical position (layout independent). Otherwise by virtual-key code.</param>
public sealed record TriggerKey(string Id, string Display, int VirtualKey, int ScanCode)
{
    /// <summary>True for keys that type a character (the backtick). The palette must keep those out of its own input box.</summary>
    public bool ProducesText => ScanCode != 0;

    /// <summary>Does this low-level keyboard event belong to this key? (extended = LLKHF_EXTENDED, e.g. numpad/right-hand variants)</summary>
    public bool Matches(int vkCode, int scanCode, bool extended) =>
        ScanCode != 0 ? scanCode == ScanCode && !extended : vkCode == VirtualKey;
}

public static class TriggerKeys
{
    /// <summary>The key left of "1" (scan code 0x29), whatever character the active layout prints on it.</summary>
    public static readonly TriggerKey Backtick = new("backtick", "` (backtick)", 0xC0, 0x29);
    public static readonly TriggerKey CapsLock = new("capslock", "Caps Lock", 0x14, 0);
    public static readonly TriggerKey ScrollLock = new("scrolllock", "Scroll Lock", 0x91, 0);
    public static readonly TriggerKey Pause = new("pause", "Pause", 0x13, 0);

    public static readonly IReadOnlyList<TriggerKey> All = new[] { Backtick, CapsLock, ScrollLock, Pause };

    public static TriggerKey Default => Backtick;

    public static TriggerKey? Parse(string? text)
    {
        var t = (text ?? "").Trim().ToLowerInvariant();
        if (t == "`") return Backtick;
        t = new string(t.Where(char.IsLetterOrDigit).ToArray());
        return t switch
        {
            "backtick" or "grave" or "graveaccent" or "backquote" or "tilde" => Backtick,
            "capslock" or "caps" => CapsLock,
            "scrolllock" or "scroll" => ScrollLock,
            "pause" or "break" or "pausebreak" => Pause,
            _ => null,
        };
    }
}
