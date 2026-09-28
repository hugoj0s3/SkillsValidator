using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions;

public interface ISkillConfigValidator
{
    /// <summary>
    /// Returns the list of errors (empty when valid). Among other rules, the PointInstructionMap
    /// must contain every point from 1 to MaxPoints.
    /// </summary>
    IReadOnlyList<string> Validate(SkillConfig config);
}
