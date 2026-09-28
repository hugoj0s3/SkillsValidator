using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.AzureSpeech;

public sealed class AzureSpeechOptions
{
    public string Key { get; set; } = "";

    public string Region { get; set; } = "";

    /// <summary>Recognition language, e.g. "en-US".</summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Silence (in ms) after which the participant's answer is considered finished and the agent replies.
    /// Longer lets the participant pause to think; shorter makes the agent reply sooner. Azure allows 100 to 5000.
    /// </summary>
    public int SilenceTimeoutMs { get; set; } = 2000;

    /// <summary>Azure voice name per gender and type, e.g. Voices[Female][Neutral] = "en-US-JennyNeural".</summary>
    public Dictionary<VoiceGender, Dictionary<VoiceType, string>> Voices { get; set; } = [];

    public string GetVoiceName(VoiceGender gender, VoiceType type) =>
        Voices.TryGetValue(gender, out var byType) && byType.TryGetValue(type, out var voice)
            ? voice
            : throw new InvalidOperationException($"No Azure voice configured for AzureSpeech:Voices:{gender}:{type}");

    /// <summary>Returns the list of missing settings (empty when valid).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Key))
            errors.Add("AzureSpeech:Key is not set. Run: dotnet user-secrets set \"AzureSpeech:Key\" \"<your key>\"");

        if (string.IsNullOrWhiteSpace(Region))
            errors.Add("AzureSpeech:Region is not set. Run: dotnet user-secrets set \"AzureSpeech:Region\" \"<region, e.g. eastus>\"");

        foreach (var gender in Enum.GetValues<VoiceGender>())
        foreach (var type in Enum.GetValues<VoiceType>())
        {
            if (!Voices.TryGetValue(gender, out var byType) || string.IsNullOrWhiteSpace(byType.GetValueOrDefault(type)))
                errors.Add($"AzureSpeech:Voices:{gender}:{type} is not set.");
        }

        return errors;
    }
}
