using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.ArtificialIntelligence;

/// <summary>The report agent: evaluates a transcript against the config's report instruction and point map.</summary>
public interface IReportEvaluator
{
    Task<SessionReportResult> EvaluateAsync(
        SkillConfig config,
        IReadOnlyList<TranscriptEntry> transcript,
        CancellationToken ct = default);
}
