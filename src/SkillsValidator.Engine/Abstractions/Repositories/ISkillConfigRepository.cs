using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.Repositories;

/// <summary>
/// Read-only access to the skill configs. Configs are hand-written JSON files, so there is no insert, update or delete.
/// </summary>
public interface ISkillConfigRepository
{
    Task<IReadOnlyList<SkillConfig>> GetAllAsync();

    Task<SkillConfig?> GetAsync(string configId);

    /// <summary>The startup load and validation outcome, including errors for invalid files.</summary>
    SkillConfigLoadResult GetLoadResult();
}
