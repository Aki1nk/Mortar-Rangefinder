namespace PubgMortarRanger.Voice;

public sealed class VoiceAnnouncementController(
    IVoiceAnnouncementService voiceAnnouncementService)
{
    public const string AnnouncementText =
        "无敌锁血已开启，子弹穿墙已开启，人物透视已开启";

    public void PlayIfEnabled(bool enabled)
    {
        if (enabled)
        {
            voiceAnnouncementService.Speak(AnnouncementText);
        }
    }
}
