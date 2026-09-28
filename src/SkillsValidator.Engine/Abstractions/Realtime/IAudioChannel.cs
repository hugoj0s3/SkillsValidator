using SkillsValidator.Engine.Models.Realtime;

namespace SkillsValidator.Engine.Abstractions.Realtime;

/// <summary>The audio pipe between the participant's browser and the server.</summary>
public interface IAudioChannel
{
    /// <summary>Microphone fragments from the participant, as they are recorded.</summary>
    IAsyncEnumerable<AudioChunk> ReadParticipantAudioAsync(CancellationToken ct);

    /// <summary>Streams the agent's synthesized voice to the browser.</summary>
    Task PlayAgentAudioAsync(IAsyncEnumerable<AudioChunk> audio, CancellationToken ct);

    /// <summary>Stops the agent's playback, e.g. when the participant starts talking.</summary>
    Task StopAgentAudioAsync();
}
