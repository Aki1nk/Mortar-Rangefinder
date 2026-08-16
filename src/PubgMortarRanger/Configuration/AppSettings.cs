using PubgMortarRanger.Core;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Configuration;

public sealed record AppSettings
{
    public double MinimumRangeMeters { get; init; } = 121;

    public double MaximumRangeMeters { get; init; } = 700;

    public int HistoryLimit { get; init; } = 20;

    public double OverlayOpacity { get; init; } = 0.94;

    public double OverlayScale { get; init; } = 1;

    public int MarkerHoldMilliseconds { get; init; } = 1500;

    public bool ClickThroughByDefault { get; init; } = true;

    public bool VoiceAnnouncementEnabled { get; init; } = true;

    public WindowPlacement? OverlayPlacement { get; init; }

    public CalibrationProfile? Calibration { get; init; }

    public IReadOnlyDictionary<HotkeyAction, HotkeyGesture> Hotkeys { get; init; } =
        HotkeyGesture.CreateDefaults();

    public static AppSettings CreateDefault() => new();
}
