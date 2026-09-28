using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.OpenAI;

public sealed class OpenAIOptions
{
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Chat model per effort level, for the interviewer and the report.
    /// A skill picks the level with its "Effort" setting (default Medium).
    /// </summary>
    public Dictionary<ModelEffort, string> Models { get; set; } = [];

    /// <summary>Returns the list of missing settings (empty when valid).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
            errors.Add("OpenAI:ApiKey is not set. Run: dotnet user-secrets set \"OpenAI:ApiKey\" \"<your key>\"");

        foreach (var effort in Enum.GetValues<ModelEffort>())
        {
            if (string.IsNullOrWhiteSpace(Models.GetValueOrDefault(effort)))
                errors.Add($"OpenAI:Models:{effort} is not set (e.g. \"gpt-4o-mini\").");
        }

        return errors;
    }
}
