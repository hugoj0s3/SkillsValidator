using SkillsValidator.Engine.Models;

namespace SkillsValidator.Tests;

/// <summary>A valid skill config for tests: 10 minutes + 2 extra, with a label for every point.</summary>
internal static class TestConfigs
{
    public static SkillConfig Create(int maxPoints = 2, int[]? points = null, ModelEffort effort = ModelEffort.Medium) => new()
    {
        Id = "test",
        Title = "Test",
        InterviewInstruction = "Ask things.",
        AgentTone = "Friendly",
        AgentName = "Agent",
        Effort = effort,
        ReportInstruction = "Evaluate.",
        MaxPoints = maxPoints,
        InterviewDurationInMinutes = 10,
        ExtraInterviewDurationInMinutes = 2,
        PointInstructionMap = (points ?? Enumerable.Range(1, maxPoints).ToArray())
            .ToDictionary(p => p, p => new PointLevel { Label = $"Level {p}" })
    };
}
