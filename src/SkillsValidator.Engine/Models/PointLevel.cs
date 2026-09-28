namespace SkillsValidator.Engine.Models;

/// <summary>The label and requirements for a single point level, e.g. 3 = "Padawan".</summary>
public sealed class PointLevel
{
    public required string Label { get; init; }

    /// <summary>Optional minimum requirements the participant must meet to reach this level.</summary>
    public string? Requirements { get; init; }
}
