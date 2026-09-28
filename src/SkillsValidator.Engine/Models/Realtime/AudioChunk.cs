namespace SkillsValidator.Engine.Models.Realtime;

/// <summary>
/// A small audio fragment (e.g. ~100 ms) streamed between the browser and the server.
/// Held in memory only; audio is never stored.
/// </summary>
public sealed record AudioChunk(byte[] Data, string Format, int SampleRate)
{
    /// <summary>Microphone audio is 16-bit mono PCM at this rate (what Azure speech recognition expects).</summary>
    public const int MicrophoneSampleRate = 16000;

    /// <summary>The agent's voice is 16-bit mono PCM at this rate (speech services must return this format).</summary>
    public const int AgentSampleRate = 24000;
}
