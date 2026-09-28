namespace SkillsValidator.Engine.Models;

/// <summary>
/// How capable (and slower/more expensive) the AI model for a skill should be.
/// Each agent provider maps a level to a model in its settings.
/// </summary>
public enum ModelEffort
{
    Low,
    Medium,
    High
}
