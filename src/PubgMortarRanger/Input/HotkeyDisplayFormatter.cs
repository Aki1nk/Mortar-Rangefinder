using System.Windows.Input;

namespace PubgMortarRanger.Input;

public static class HotkeyDisplayFormatter
{
    public static string Format(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add($"{KeyName(gesture.VirtualKey)}键");
        return string.Join(" + ", parts);
    }

    private static string KeyName(int virtualKey)
    {
        return KeyInterop.KeyFromVirtualKey(virtualKey) switch
        {
            Key.Escape => "Esc",
            Key.Return => "Enter",
            Key.Space => "空格",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.OemPlus or Key.Add => "+",
            Key.OemMinus or Key.Subtract => "-",
            Key.OemComma => ",",
            Key.OemPeriod or Key.Decimal => ".",
            Key.Oem1 => ";",
            Key.Oem2 or Key.Divide => "/",
            Key.Oem3 => "`",
            Key.Oem4 => "[",
            Key.Oem5 => "\\",
            Key.Oem6 => "]",
            Key.Oem7 => "'",
            Key.Multiply => "*",
            _ when virtualKey is >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            var key => key.ToString()
        };
    }
}
