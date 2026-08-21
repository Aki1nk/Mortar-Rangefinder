using PubgMortarRanger;
using PubgMortarRanger.Configuration;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Voice;

public sealed class VoiceAnnouncementFeatureTests
{
    [Fact]
    public void VoiceAnnouncementFeature_HasEnabledSettingAndCtrlF12Hotkey()
    {
        var enabledProperty = typeof(AppSettings).GetProperty("VoiceAnnouncementEnabled");

        Assert.NotNull(enabledProperty);
        Assert.True((bool)enabledProperty.GetValue(AppSettings.CreateDefault())!);
        Assert.True(Enum.TryParse<HotkeyAction>("PlayVoiceAnnouncement", out var action));
        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.Control, 0x7B),
            AppSettings.CreateDefault().Hotkeys[action]);
    }

    [Fact]
    public void Defaults_IncludeRecalibrateHotkey()
    {
        Assert.True(Enum.TryParse<HotkeyAction>("Recalibrate", out var action));
        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.Control, 0x77),
            AppSettings.CreateDefault().Hotkeys[action]);
    }

    [Fact]
    public void ApplicationAssembly_ProvidesLocalVoiceAnnouncementController()
    {
        var controller = typeof(App).Assembly.GetType(
            "PubgMortarRanger.Voice.VoiceAnnouncementController");

        Assert.NotNull(controller);
        Assert.NotNull(controller.GetMethod("PlayIfEnabled"));
    }
}
