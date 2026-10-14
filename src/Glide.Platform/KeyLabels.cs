using Glide.Core;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Glide.Platform;

/// <summary>
/// Shortcut text as printed on the user's own keyboard. Glide matches keys by position, so on a
/// Spanish keyboard Win+[ is the key right of P, which shows "`" there; on AZERTY, Win+Q is "A".
/// </summary>
public static unsafe class KeyLabels
{
    public static string Describe(KeyCombo combo) => combo.Modifiers.Prefix() + Label(combo.KeyCode);

    public static string Label(uint keyCode)
    {
        if (KeyCode.ScanCode(keyCode) is not { } scan) return KeyCode.Name(keyCode);
        var layout = PInvoke.GetKeyboardLayout(0);
        var virtualKey = PInvoke.MapVirtualKeyEx(scan, MAP_VIRTUAL_KEY_TYPE.MAPVK_VSC_TO_VK_EX, layout);
        var state = stackalloc byte[256];
        var buffer = stackalloc char[8];
        // Flag 4: don't disturb the dead-key state of whatever the user is typing.
        var length = PInvoke.ToUnicodeEx(virtualKey, scan, state, buffer, 8, 4, layout);
        // A dead key (like Spanish "`") returns -1 with its spacing character in the buffer.
        if (length == 0 || char.IsControl(buffer[0]) || char.IsWhiteSpace(buffer[0])) return KeyCode.Name(keyCode);
        var label = char.ToUpperInvariant(buffer[0]);
        return label == '+' ? "Plus" : label.ToString();
    }
}
