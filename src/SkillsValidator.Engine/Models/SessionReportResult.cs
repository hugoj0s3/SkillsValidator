namespace SkillsValidator.Engine.Models;

/// <summary>The final evaluation produced by the report agent.</summary>
public sealed class SessionReportResult
{
    /// <summary>Score from 1 to <see cref="MaxPoints"/>.</summary>
    public int Score { get; init; }

    public int MaxPoints { get; init; }

    /// <summary>The <see cref="PointLevel.Label"/> that matches <see cref="Score"/>.</summary>
    public required string Label { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<string> StrongPoints { get; init; } = [];

    public IReadOnlyList<string> ImprovementAreas { get; init; } = [];
}
