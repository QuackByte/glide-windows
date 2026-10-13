namespace Glide.Core;

/// <summary>Modifier keys of a shortcut, independent of Win32.</summary>
[Flags]
public enum KeyModifiers : uint
{
    None = 0,
    Control = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Win = 1 << 3,
}

public static class KeyModifiersExtensions
{
    /// <summary>Names in the order Windows shows them, e.g. "Win+Ctrl+Shift+".</summary>
    public static string Prefix(this KeyModifiers modifiers) =>
        (modifiers.HasFlag(KeyModifiers.Win) ? "Win+" : "") + (modifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : "")
        + (modifiers.HasFlag(KeyModifiers.Alt) ? "Alt+" : "") + (modifiers.HasFlag(KeyModifiers.Shift) ? "Shift+" : "");
}

/// <summary>A shortcut: a Windows virtual-key code plus modifiers.</summary>
public readonly record struct KeyCombo(uint KeyCode, KeyModifiers Modifiers)
{
    /// <summary>Display text using US key names, e.g. "Win+Ctrl+←".</summary>
    public override string ToString() => Modifiers.Prefix() + Glide.Core.KeyCode.Name(KeyCode);

    /// <summary>Compact persisted form, e.g. "8:37".</summary>
    public string StorageValue => $"{(uint)Modifiers}:{KeyCode}";

    public static KeyCombo? FromStorage(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 2 || !uint.TryParse(parts[0], out var modifiers) || !uint.TryParse(parts[1], out var keyCode)) return null;
        return new KeyCombo(keyCode, (KeyModifiers)modifiers);
    }
}

/// <summary>Windows virtual-key codes (VK_*) for the keys Glide names.</summary>
public static class KeyCode
{
    public const uint Tab = 0x09;
    public const uint Return = 0x0D;
    public const uint Escape = 0x1B;
    public const uint Space = 0x20;
    public const uint PageUp = 0x21;
    public const uint PageDown = 0x22;
    public const uint End = 0x23;
    public const uint Home = 0x24;
    public const uint LeftArrow = 0x25;
    public const uint UpArrow = 0x26;
    public const uint RightArrow = 0x27;
    public const uint DownArrow = 0x28;
    public const uint Delete = 0x2E;
    public const uint Backspace = 0x08;
    public const uint F1 = 0x70;
    public const uint F24 = 0x87;
    /// <summary>VK_OEM_4 and VK_OEM_6: the [ and ] keys on a US layout.</summary>
    public const uint LeftBracket = 0xDB;
    public const uint RightBracket = 0xDD;

    public static uint Letter(char letter) => char.ToUpperInvariant(letter);

    public static readonly uint C = Letter('C'), D = Letter('D'), F = Letter('F'), L = Letter('L'), R = Letter('R'), T = Letter('T'), V = Letter('V'), W = Letter('W');

    public static readonly IReadOnlySet<uint> Arrows = new HashSet<uint> { LeftArrow, RightArrow, DownArrow, UpArrow };
    /// <summary>The number row, 1 through 9.</summary>
    public static readonly IReadOnlyList<uint> Digits = [.. Enumerable.Range('1', 9).Select(c => (uint)c)];

    private static readonly Dictionary<uint, string> names = new()
    {
        [LeftArrow] = "←", [RightArrow] = "→", [DownArrow] = "↓", [UpArrow] = "↑",
        [Home] = "Home", [End] = "End", [PageUp] = "PgUp", [PageDown] = "PgDn",
        [Return] = "Enter", [Tab] = "Tab", [Space] = "Space", [Backspace] = "Backspace", [Escape] = "Esc", [Delete] = "Del",
        [0xBD] = "-", [0xBB] = "=", [LeftBracket] = "[", [RightBracket] = "]", [0xBA] = ";", [0xDE] = "'", [0xDC] = "\\",
        [0xBC] = ",", [0xBE] = ".", [0xBF] = "/", [0xC0] = "`",
    };

    public static bool IsFunctionKey(uint keyCode) => keyCode is >= F1 and <= F24;

    public static string Name(uint keyCode)
    {
        if (names.TryGetValue(keyCode, out var name)) return name;
        if (keyCode is >= '0' and <= '9' or >= 'A' and <= 'Z') return ((char)keyCode).ToString();
        if (IsFunctionKey(keyCode)) return $"F{keyCode - F1 + 1}";
        return $"Key {keyCode}";
    }
}
