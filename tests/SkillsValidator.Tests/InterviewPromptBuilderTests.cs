using SkillsValidator.Engine;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Tests;

public class InterviewPromptBuilderTests
{
    // 10 minutes + 2 extra minutes.
    private readonly SkillConfig config = TestConfigs.Create();
    private readonly DateTimeOffset startedAt = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Before_the_duration_the_interview_is_in_progress()
    {
        var instruction = BuildAt(minute: 3.5);

        Assert.Contains("3 of 10 minutes have passed (7 minutes left)", instruction);
        Assert.Contains("Do not mention extra time", instruction);
    }

    [Fact]
    public void First_agent_turn_after_the_duration_announces_the_extra_time()
    {
        var instruction = BuildAt(minute: 10.5, agentSpokeAtMinute: 9);

        Assert.Contains("Start your reply by telling the participant", instruction);
        Assert.Contains("they have\n2 extra minutes to finish", instruction.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Once_announced_the_extra_time_is_not_announced_again()
    {
        var instruction = BuildAt(minute: 11.5, agentSpokeAtMinute: 10.5);

        Assert.Contains("(1 of 2 extra minutes left)", instruction);
        Assert.Contains("The participant already knows", instruction);
    }

    [Fact]
    public void After_the_extra_time_the_agent_closes()
    {
        var instruction = BuildAt(minute: 12.5);

        Assert.Contains("Time is up", instruction);
    }

    [Fact]
    public void System_prompt_has_the_fixed_instructions_without_time()
    {
        var prompt = InterviewPromptBuilder.Build(config, Session(agentSpokeAtMinute: null));

        Assert.Contains("talking with Ana", prompt);
        Assert.Contains(config.InterviewInstruction, prompt);
        Assert.DoesNotContain("minutes", prompt); // time is sent separately, as the last message
        Assert.DoesNotContain("{", prompt);       // every placeholder was filled
    }

    private string BuildAt(double minute, double? agentSpokeAtMinute = null) =>
        InterviewPromptBuilder.BuildTimeInstruction(config, Session(agentSpokeAtMinute), startedAt.AddMinutes(minute));

    private InterviewSession Session(double? agentSpokeAtMinute) => new()
    {
        Id = "s1",
        ConfigId = config.Id,
        ParticipantName = "Ana",
        StartedAt = startedAt,
        Transcriptions = agentSpokeAtMinute is { } minute
            ? [new TranscriptEntry(Speaker.Agent, "A question?", startedAt.AddMinutes(minute))]
            : []
    };
}
