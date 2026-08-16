using PubgMortarRanger;
using PubgMortarRanger.Configuration;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Voice;

public sealed class VoiceAnnouncementFeatureTests
{
    [Fact]
    public void VoiceAnnouncementFeature_HasEnabledSettingAndF12Hotkey()
    {
        var enabledProperty = typeof(AppSettings).GetProperty("VoiceAnnouncementEnabled");

        Assert.NotNull(enabledProperty);
        Assert.True((bool)enabledProperty.GetValue(AppSettings.CreateDefault())!);
        Assert.True(Enum.TryParse<HotkeyAction>("PlayVoiceAnnouncement", out var action));
        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.None, 0x7B),
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
