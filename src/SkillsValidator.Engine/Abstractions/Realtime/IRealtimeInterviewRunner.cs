using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.Realtime;

/// <summary>
/// The conversation loop: participant audio -> speech-to-text -> agent -> text-to-speech -> browser.
/// Final transcription segments and agent turns are appended to the transcript.
/// When the agent closes the interview, it calls ISessionInterviewService.FinishInterviewSessionAsync.
/// </summary>
public interface IRealtimeInterviewRunner
{
    /// <param name="onPartialTranscript">
    /// Receives what the participant is saying while they speak (live caption); an empty string clears it.
    /// </param>
    Task RunAsync(
        string sessionId,
        SkillConfig config,
        IAudioChannel channel,
        Func<string, Task>? onPartialTranscript,
        CancellationToken ct);
}
