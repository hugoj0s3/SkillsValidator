namespace SkillsValidator.Engine.Models;

/// <summary>
/// A skill interview configuration. Loaded read-only from a hand-written JSON file in data/skills/.
/// </summary>
public sealed class SkillConfig
{
    /// <summary>Unique id of the config (e.g. "csharp").</summary>
    public required string Id { get; init; }

    /// <summary>The title of the skill, e.g. "C# Programming". Shown in the start page dropdown.</summary>
    public required string Title { get; init; }

    /// <summary>What the agent should ask, which points matter most and how to conduct the interview.</summary>
    public required string InterviewInstruction { get; init; }

    /// <summary>How the agent should behave, e.g. friendly or professional.</summary>
    public required string AgentTone { get; init; }

    /// <summary>The name the agent uses to introduce itself.</summary>
    public required string AgentName { get; init; }

    public VoiceGender AgentVoiceGender { get; init; }

    public VoiceType AgentVoiceType { get; init; }

    /// <summary>Which AI model the interviewer and the report use (mapped to a model in appsettings). Optional.</summary>
    public ModelEffort Effort { get; init; } = ModelEffort.Medium;

    /// <summary>How the transcript should be evaluated and what should (or should not) be reported.</summary>
    public required string ReportInstruction { get; init; }

    /// <summary>Maximum points for this skill. The score ranges from 1 to MaxPoints.</summary>
    public int MaxPoints { get; init; }

    /// <summary>Planned interview duration. Enforced only through the agent instructions.</summary>
    public int InterviewDurationInMinutes { get; init; }

    /// <summary>Extra minutes the agent grants once the interview duration has passed.</summary>
    public int ExtraInterviewDurationInMinutes { get; init; }

    /// <summary>One entry for every point from 1 to MaxPoints.</summary>
    public required Dictionary<int, PointLevel> PointInstructionMap { get; init; }
}
