using SkillsValidator.Engine.Implementations;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Tests;

public class SkillConfigValidatorTests
{
    private readonly SkillConfigValidator validator = new();

    [Fact]
    public void Valid_config_has_no_errors()
    {
        Assert.Empty(validator.Validate(TestConfigs.Create(maxPoints: 3, points: [1, 2, 3])));
    }

    [Fact]
    public void Missing_point_is_reported()
    {
        var errors = validator.Validate(TestConfigs.Create(maxPoints: 3, points: [1, 3]));

        Assert.Contains("PointInstructionMap is missing point 2 (must contain 1..3)", errors);
    }

    [Fact]
    public void Point_outside_range_is_reported()
    {
        var errors = validator.Validate(TestConfigs.Create(maxPoints: 2, points: [1, 2, 5]));

        Assert.Contains("PointInstructionMap has point 5 outside 1..2", errors);
    }
}
