using Microsoft.Extensions.AI;
using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.OpenAI;

namespace SkillsValidator.Tests;

public class OpenAIReportEvaluatorTests
{
    private readonly SkillConfig config = TestConfigs.Create(maxPoints: 3);

    private readonly IReadOnlyList<TranscriptEntry> transcript =
    [
        new(Speaker.Agent, "What is async/await?", DateTimeOffset.UtcNow),
        new(Speaker.Participant, "It lets you wait for I/O without blocking a thread.", DateTimeOffset.UtcNow)
    ];

    [Fact]
    public async Task Parses_the_model_json_and_takes_the_label_from_the_config()
    {
        var chat = new FakeChatClient(
            """{"Score":2,"Summary":"Solid basics.","StrongPoints":["async"],"ImprovementAreas":["GC"]}""");

        var result = await new OpenAIReportEvaluator(AllLevels(chat)).EvaluateAsync(config, transcript);

        Assert.Equal(2, result.Score);
        Assert.Equal(3, result.MaxPoints);
        Assert.Equal("Level 2", result.Label);
        Assert.Equal("Solid basics.", result.Summary);
        Assert.Equal(["async"], result.StrongPoints);
        Assert.Equal(["GC"], result.ImprovementAreas);

        // The prompt contains the levels and the transcript.
        Assert.Contains("- 2 = Level 2", chat.LastSystemPrompt);
        Assert.Contains("Participant: It lets you wait", chat.LastUserMessage);
    }

    [Fact]
    public async Task Score_out_of_range_is_clamped()
    {
        var chat = new FakeChatClient(
            """{"Score":9,"Summary":"Great.","StrongPoints":[],"ImprovementAreas":[]}""");

        var result = await new OpenAIReportEvaluator(AllLevels(chat)).EvaluateAsync(config, transcript);

        Assert.Equal(3, result.Score);
        Assert.Equal("Level 3", result.Label);
    }

    [Fact]
    public async Task No_participant_answers_gives_the_lowest_score_without_calling_the_model()
    {
        var chat = new FakeChatClient("not called");
        IReadOnlyList<TranscriptEntry> onlyAgent = [new(Speaker.Agent, "Hello?", DateTimeOffset.UtcNow)];

        var result = await new OpenAIReportEvaluator(AllLevels(chat)).EvaluateAsync(config, onlyAgent);

        Assert.Equal(1, result.Score);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task Uses_the_model_for_the_skill_effort()
    {
        var lowModel = new FakeChatClient("""{"Score":1,"Summary":"Low.","StrongPoints":[],"ImprovementAreas":[]}""");
        var highModel = new FakeChatClient("""{"Score":3,"Summary":"High.","StrongPoints":[],"ImprovementAreas":[]}""");
        var clients = new OpenAIChatClients(new Dictionary<ModelEffort, IChatClient>
        {
            [ModelEffort.Low] = lowModel,
            [ModelEffort.Medium] = lowModel,
            [ModelEffort.High] = highModel
        });
        var hardSkill = TestConfigs.Create(maxPoints: 3, effort: ModelEffort.High);

        var result = await new OpenAIReportEvaluator(clients).EvaluateAsync(hardSkill, transcript);

        Assert.Equal("High.", result.Summary);
        Assert.Equal(1, highModel.Calls);
        Assert.Equal(0, lowModel.Calls);
    }

    private static OpenAIChatClients AllLevels(IChatClient chat) =>
        new(Enum.GetValues<ModelEffort>().ToDictionary(effort => effort, _ => chat));

    private sealed class FakeChatClient(string json) : IChatClient
    {
        public int Calls { get; private set; }
        public string LastSystemPrompt { get; private set; } = "";
        public string LastUserMessage { get; private set; } = "";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            var list = messages.ToList();
            LastSystemPrompt = list.First(m => m.Role == ChatRole.System).Text;
            LastUserMessage = list.Last(m => m.Role == ChatRole.User).Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, json)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
