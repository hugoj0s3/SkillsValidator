using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine;

/// <summary>
/// Builds the interviewer's system prompt. Shared by every agent implementation,
/// so all agents follow the same instructions.
/// </summary>
public static class InterviewPromptBuilder
{
    /// <summary>The system prompt: who the agent is, how to interview, the tone and the skill's instructions.</summary>
    public static string Build(SkillConfig config, InterviewSession session)
    {
        var baseInstructions = InterviewBaseInstructions.Text
            .Replace("{AgentName}", config.AgentName)
            .Replace("{Title}", config.Title)
            .Replace("{ParticipantName}", session.ParticipantName);

        return $"""
                {baseInstructions}

                Tone:
                {config.AgentTone}

                Interview instructions:
                {config.InterviewInstruction}
                """;
    }

    /// <summary>
    /// The time note for the agent's next turn. Agents send it as the last message of the conversation:
    /// models follow the latest message much more closely than a long system prompt.
    /// The server knows the time and the model is bad at computing it, so the phase is decided here:
    /// in progress, extra time starts (announce it), extra time, time is up.
    /// </summary>
    public static string BuildTimeInstruction(SkillConfig config, InterviewSession session, DateTimeOffset now)
    {
        var elapsed = now - session.StartedAt;
        var duration = TimeSpan.FromMinutes(config.InterviewDurationInMinutes);
        var end = duration + TimeSpan.FromMinutes(config.ExtraInterviewDurationInMinutes);

        string instruction;
        TimeSpan remaining;

        if (elapsed < duration)
        {
            instruction = InterviewBaseInstructions.TimeInProgress;
            remaining = duration - elapsed;
        }
        else if (elapsed < end)
        {
            // The extra time is announced in the agent's first turn after the planned duration.
            var announced = session.Transcriptions.Any(e =>
                e.Speaker == Speaker.Agent && e.Timestamp >= session.StartedAt + duration);

            instruction = announced ? InterviewBaseInstructions.TimeExtra : InterviewBaseInstructions.TimeExtraStarts;
            remaining = end - elapsed;
        }
        else
        {
            instruction = InterviewBaseInstructions.TimeIsUp;
            remaining = TimeSpan.Zero;
        }

        return instruction
            .Replace("{ElapsedMinutes}", ((int)Math.Max(0, elapsed.TotalMinutes)).ToString())
            .Replace("{RemainingMinutes}", ((int)Math.Ceiling(remaining.TotalMinutes)).ToString())
            .Replace("{InterviewDurationInMinutes}", config.InterviewDurationInMinutes.ToString())
            .Replace("{ExtraInterviewDurationInMinutes}", config.ExtraInterviewDurationInMinutes.ToString());
    }
}
