using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.ArtificialIntelligence;

/// <summary>
/// The LLM interviewer. Its system prompt comes from <see cref="InterviewPromptBuilder.Build"/>, and a short
/// time note from <see cref="InterviewPromptBuilder.BuildTimeInstruction"/> is sent as the last message.
/// </summary>
public interface IRealtimeInterviewAgent
{
    /// <summary>
    /// Streams the agent's next turn as text fragments, based on the transcript in <paramref name="session"/>.
    /// When the transcript is empty, the agent opens the interview.
    /// </summary>
    IAsyncEnumerable<string> RespondAsync(
        SkillConfig config,
        InterviewSession session,
        CancellationToken ct = default);
}
