namespace SkillsValidator.Engine.Models;

/// <summary>All validation (or JSON parse) errors of one config file.</summary>
public sealed record ConfigError(string FileName, IReadOnlyList<string> Messages);
