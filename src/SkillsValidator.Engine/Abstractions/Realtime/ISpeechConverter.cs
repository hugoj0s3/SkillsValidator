using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.Models.Realtime;

namespace SkillsValidator.Engine.Abstractions.Realtime;

/// <summary>Speech-to-text and text-to-speech.</summary>
public interface ISpeechConverter
{
    /// <summary>
    /// Transcribes in the background while the audio is still being recorded
    /// (16-bit mono PCM at <see cref="AudioChunk.MicrophoneSampleRate"/>).
    /// Returns partial segments while the participant speaks (an empty partial means they just started speaking),
    /// and a final segment after a short silence.
    /// </summary>
    IAsyncEnumerable<TranscriptionSegment> ToTranscriptionAsync(
        IAsyncEnumerable<AudioChunk> speech,
        CancellationToken ct = default);

    /// <summary>Synthesizes the text with the agent voice from the skill config.</summary>
    /// <param name="speakingStyle">Optional instructions for how to speak, e.g. the config's AgentTone.</param>
    IAsyncEnumerable<AudioChunk> ToSpeechAsync(
        string text,
        VoiceGender gender,
        VoiceType type,
        string? speakingStyle,
        CancellationToken ct = default);
}
