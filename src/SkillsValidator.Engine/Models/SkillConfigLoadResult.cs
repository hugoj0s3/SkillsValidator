namespace SkillsValidator.Engine.Models;

/// <summary>
/// The outcome of loading and validating all skill configs at startup.
/// If <see cref="HasErrors"/> or <see cref="NoConfigFound"/> is true, the app shows only the error page.
/// </summary>
public sealed class SkillConfigLoadResult
{
    public IReadOnlyList<SkillConfig> Configs { get; init; } = [];

    public IReadOnlyList<ConfigError> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;

    public bool NoConfigFound => Configs.Count == 0 && Errors.Count == 0;
}
