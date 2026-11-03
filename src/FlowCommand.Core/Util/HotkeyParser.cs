namespace FlowCommand.Core.Util;

[Flags]
public enum HotkeyModifiers : uint { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

public sealed record Hotkey(HotkeyModifiers Modifiers, uint VirtualKey, string Display);
public sealed record HotkeyParseResult(Hotkey? Hotkey, string? Error) { public bool Ok => Hotkey != null; }

/// <summary>Parses strings like "Ctrl+Alt+Space" into RegisterHotKey arguments (no WPF dependency).</summary>
public static class HotkeyParser
{
    private static readonly Dictionary<string, uint> Keys = BuildKeys();

    private static Dictionary<string, uint> BuildKeys()
    {
        var d = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'A'; c <= 'Z'; c++) d[c.ToString()] = c;
        for (var c = '0'; c <= '9'; c++) d[c.ToString()] = c;
        for (var i = 1; i <= 24; i++) d["F" + i] = (uint)(0x70 + i - 1);
        d["Space"] = 0x20; d["Tab"] = 0x09; d["Enter"] = 0x0D; d["Insert"] = 0x2D; d["Delete"] = 0x2E;
        d["Home"] = 0x24; d["End"] = 0x23; d["PageUp"] = 0x21; d["PageDown"] = 0x22;
        d["Left"] = 0x25; d["Up"] = 0x26; d["Right"] = 0x27; d["Down"] = 0x28;
        d["`"] = 0xC0; d[";"] = 0xBA; d["="] = 0xBB; d[","] = 0xBC; d["-"] = 0xBD; d["."] = 0xBE;
        d["/"] = 0xBF; d["["] = 0xDB; d["\\"] = 0xDC; d["]"] = 0xDD; d["'"] = 0xDE;
        return d;
    }

    public static HotkeyParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new(null, "Hotkey is empty.");
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(p => p.Length == 0))
            return new(null, "Use the format Ctrl+Alt+Space.");

        var mods = HotkeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= HotkeyModifiers.Control; break;
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win": case "windows": case "super": mods |= HotkeyModifiers.Win; break;
                default: return new(null, $"Unknown modifier '{parts[i]}'.");
            }
        }

        var keyName = parts[^1];
        if (!Keys.TryGetValue(keyName, out var vk)) return new(null, $"Unknown key '{keyName}'.");
        var isFunctionKey = vk >= 0x70 && vk <= 0x87;
        if (mods == HotkeyModifiers.None && !isFunctionKey)
            return new(null, "Add at least one modifier (Ctrl, Alt, Shift or Win).");

        var display = string.Join("+",
            new[] {
                mods.HasFlag(HotkeyModifiers.Control) ? "Ctrl" : null,
                mods.HasFlag(HotkeyModifiers.Alt) ? "Alt" : null,
                mods.HasFlag(HotkeyModifiers.Shift) ? "Shift" : null,
                mods.HasFlag(HotkeyModifiers.Win) ? "Win" : null,
                keyName.Length == 1 ? keyName.ToUpperInvariant() : keyName
            }.Where(x => x != null));
        return new(new Hotkey(mods, vk, display), null);
    }
}
