using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations;

public sealed class SkillConfigValidator : ISkillConfigValidator
{
    public IReadOnlyList<string> Validate(SkillConfig config)
    {
        var errors = new List<string>();

        RequireText(config.Id, nameof(config.Id), errors);
        RequireText(config.Title, nameof(config.Title), errors);
        RequireText(config.InterviewInstruction, nameof(config.InterviewInstruction), errors);
        RequireText(config.AgentTone, nameof(config.AgentTone), errors);
        RequireText(config.AgentName, nameof(config.AgentName), errors);
        RequireText(config.ReportInstruction, nameof(config.ReportInstruction), errors);

        if (config.InterviewDurationInMinutes <= 0)
            errors.Add($"{nameof(config.InterviewDurationInMinutes)} must be greater than 0");

        if (config.ExtraInterviewDurationInMinutes < 0)
            errors.Add($"{nameof(config.ExtraInterviewDurationInMinutes)} must be 0 or greater");

        if (config.MaxPoints <= 0)
        {
            errors.Add($"{nameof(config.MaxPoints)} must be greater than 0");
            return errors;
        }

        var map = config.PointInstructionMap;

        for (var point = 1; point <= config.MaxPoints; point++)
        {
            if (!map.TryGetValue(point, out var level))
                errors.Add($"PointInstructionMap is missing point {point} (must contain 1..{config.MaxPoints})");
            else if (string.IsNullOrWhiteSpace(level.Label))
                errors.Add($"PointInstructionMap point {point} has no Label");
        }

        foreach (var point in map.Keys.Where(p => p < 1 || p > config.MaxPoints).Order())
            errors.Add($"PointInstructionMap has point {point} outside 1..{config.MaxPoints}");

        return errors;
    }

    private static void RequireText(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{name} is required");
    }
}
