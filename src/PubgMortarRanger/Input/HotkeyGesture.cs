using System.Collections.ObjectModel;

namespace PubgMortarRanger.Input;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed record HotkeyGesture(
    HotkeyModifiers Modifiers,
    int VirtualKey,
    bool IsGlobal = true)
{
    private static readonly IReadOnlyDictionary<HotkeyAction, HotkeyGesture>
        DefaultBindings = new ReadOnlyDictionary<HotkeyAction, HotkeyGesture>(
            new Dictionary<HotkeyAction, HotkeyGesture>
            {
                [HotkeyAction.RecordMortar] = new(HotkeyModifiers.None, 0x75),
                [HotkeyAction.RecordTarget] = new(HotkeyModifiers.None, 0x76),
                [HotkeyAction.BeginClickSelection] = new(HotkeyModifiers.None, 0x77),
                [HotkeyAction.BeginCalibration] = new(HotkeyModifiers.None, 0x78),
                [HotkeyAction.ClearMeasurement] = new(HotkeyModifiers.None, 0x79),
                [HotkeyAction.ToggleOverlay] = new(HotkeyModifiers.None, 0x7A),
                [HotkeyAction.ToggleClickThrough] = new(HotkeyModifiers.Control, 0x7A),
                [HotkeyAction.CancelCurrent] = new(
                    HotkeyModifiers.None,
                    0x1B,
                    IsGlobal: false),
                [HotkeyAction.PlayVoiceAnnouncement] = new(
                    HotkeyModifiers.None,
                    0x7B),
                [HotkeyAction.Recalibrate] = new(
                    HotkeyModifiers.Control,
                    0x77)
            });

    public static IReadOnlyDictionary<HotkeyAction, HotkeyGesture> CreateDefaults() =>
        DefaultBindings;
}
