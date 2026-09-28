using System.Text.Json;
using System.Text.Json.Serialization;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Tests;

public class SkillConfigJsonTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Deserializes_config_with_point_map_and_enums()
    {
        const string json =
            """
            {
              "Id": "star-wars",
              "Title": "Star Wars Lore",
              "InterviewInstruction": "Ask about the Jedi Order and the Force.",
              "AgentTone": "Friendly, like a wise Jedi master.",
              "AgentName": "Master Oren",
              "AgentVoiceGender": "Male",
              "AgentVoiceType": "Deep",
              "ReportInstruction": "Focus on depth of lore knowledge.",
              "MaxPoints": 3,
              "InterviewDurationInMinutes": 10,
              "ExtraInterviewDurationInMinutes": 2,
              "PointInstructionMap": {
                "1": { "Label": "Youngling" },
                "2": { "Label": "Padawan", "Requirements": "Knows the main films." },
                "3": { "Label": "Jedi Master", "Requirements": "Knows the Expanded Universe." }
              }
            }
            """;

        var config = JsonSerializer.Deserialize<SkillConfig>(json, Options)!;

        Assert.Equal("Star Wars Lore", config.Title);
        Assert.Equal(VoiceGender.Male, config.AgentVoiceGender);
        Assert.Equal(VoiceType.Deep, config.AgentVoiceType);
        Assert.Equal(3, config.PointInstructionMap.Count);
        Assert.Equal("Padawan", config.PointInstructionMap[2].Label);
        Assert.Null(config.PointInstructionMap[1].Requirements);
        Assert.Equal(ModelEffort.Medium, config.Effort); // "Effort" is optional
    }
}
