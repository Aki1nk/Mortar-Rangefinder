namespace PubgMortarRanger.Voice;

public sealed class SapiVoiceAnnouncementService : IVoiceAnnouncementService
{
    public void Speak(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        _ = Task.Run(() =>
        {
            try
            {
                var sapiVoiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (sapiVoiceType is null)
                {
                    return;
                }

                dynamic voice = Activator.CreateInstance(sapiVoiceType)!;
                SelectSimplifiedChineseVoice(voice);
                voice.Speak(text);
            }
            catch
            {
            }
        });
    }

    private static void SelectSimplifiedChineseVoice(dynamic voice)
    {
        dynamic voices = voice.GetVoices();
        for (var index = 0; index < (int)voices.Count; index++)
        {
            dynamic candidate = voices.Item(index);
            var language = (string)candidate.GetAttribute("Language");
            if (language.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Contains("804", StringComparer.OrdinalIgnoreCase))
            {
                voice.Voice = candidate;
                return;
            }
        }
    }
}
