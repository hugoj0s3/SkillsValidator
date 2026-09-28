using System.Text;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine;

/// <summary>
/// Builds the report agent's prompt. Shared by every agent implementation,
/// so all agents evaluate with the same instructions.
/// </summary>
public static class ReportPromptBuilder
{
    public static string BuildSystemPrompt(SkillConfig config)
    {
        var levels = new StringBuilder();
        foreach (var (point, level) in config.PointInstructionMap.OrderBy(p => p.Key))
        {
            levels.Append($"- {point} = {level.Label}");
            if (!string.IsNullOrWhiteSpace(level.Requirements))
                levels.Append($": {level.Requirements}");
            levels.AppendLine();
        }

        return $"""
                You evaluate a voice interview that validated the skill "{config.Title}".
                Score the participant from 1 to {config.MaxPoints} using these levels.
                Each level lists the minimum requirements to reach it:
                {levels}
                - The score is the highest level whose minimum requirements the participant clearly met.
                - Base the evaluation only on what the participant said in the transcript.
                - The transcript comes from speech recognition, so ignore small transcription mistakes.
                - Summary: a few sentences about the overall performance.
                - StrongPoints and ImprovementAreas: 2 to 5 short, concrete items each.

                Evaluation instructions:
                {config.ReportInstruction}
                """;
    }

    public static string BuildTranscript(IReadOnlyList<TranscriptEntry> transcript)
    {
        var text = new StringBuilder();
        foreach (var entry in transcript)
            text.AppendLine($"{(entry.Speaker == Speaker.Agent ? "Interviewer" : "Participant")}: {entry.Text}");

        return text.ToString();
    }

    /// <summary>
    /// Turns the agent's raw evaluation into the final result: the score is clamped to 1..MaxPoints
    /// and the label comes from the config, never from the model.
    /// </summary>
    public static SessionReportResult ToResult(
        SkillConfig config,
        int score,
        string summary,
        IReadOnlyList<string> strongPoints,
        IReadOnlyList<string> improvementAreas)
    {
        score = Math.Clamp(score, 1, config.MaxPoints);

        return new SessionReportResult
        {
            Score = score,
            MaxPoints = config.MaxPoints,
            Label = config.PointInstructionMap[score].Label,
            Summary = summary.Trim(),
            StrongPoints = strongPoints,
            ImprovementAreas = improvementAreas
        };
    }

    /// <summary>The result when the participant never answered, so there is nothing to evaluate.</summary>
    public static SessionReportResult NoAnswers(SkillConfig config) =>
        ToResult(config, 1, "The participant did not answer any question, so the skill could not be evaluated.", [], []);
}
