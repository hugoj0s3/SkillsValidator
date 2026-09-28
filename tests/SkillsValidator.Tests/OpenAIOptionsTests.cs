using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.OpenAI;

namespace SkillsValidator.Tests;

public class OpenAIOptionsTests
{
    [Fact]
    public void Every_effort_level_needs_a_model()
    {
        var options = new OpenAIOptions
        {
            ApiKey = "key",
            Models = new() { [ModelEffort.Low] = "gpt-4o-mini", [ModelEffort.Medium] = "gpt-4.1-mini" }
        };

        Assert.Equal(["OpenAI:Models:High is not set (e.g. \"gpt-4o-mini\")."], options.Validate());
    }

    [Fact]
    public void Complete_settings_are_valid()
    {
        var options = new OpenAIOptions
        {
            ApiKey = "key",
            Models = new()
            {
                [ModelEffort.Low] = "gpt-4o-mini",
                [ModelEffort.Medium] = "gpt-4.1-mini",
                [ModelEffort.High] = "gpt-4.1"
            }
        };

        Assert.Empty(options.Validate());
    }
}
