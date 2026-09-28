using Microsoft.Extensions.Logging.Abstractions;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Implementations;
using SkillsValidator.Engine.Implementations.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Tests;

public class SessionInterviewServiceTests
{
    private readonly SkillConfig config = TestConfigs.Create();
    private readonly TaskCompletionSource<SessionReportResult> evaluation = new();
    private readonly SessionInterviewService service;

    public SessionInterviewServiceTests()
    {
        service = new SessionInterviewService(
            new FakeConfigRepository(config),
            new InMemorySessionRepository(),
            new InMemorySessionTranscriptRepository(),
            new InMemorySessionResultRepository(),
            new FakeEvaluator(evaluation.Task),
            TimeProvider.System,
            NullLogger<SessionInterviewService>.Instance);
    }

    [Fact]
    public async Task Unknown_session_returns_null()
    {
        Assert.Null(await service.GetSessionAsync("missing"));
    }

    [Fact]
    public async Task Session_goes_from_running_to_finished_to_reported()
    {
        var sessionId = await service.StartInterviewSessionAsync("Ana", config);
        Assert.Equal(SessionState.Running, (await service.GetSessionAsync(sessionId))!.State);

        await service.FinishInterviewSessionAsync(sessionId);
        var finished = (await service.GetSessionAsync(sessionId))!;
        Assert.Equal(SessionState.Finished, finished.State);
        Assert.Null(finished.Result);

        evaluation.SetResult(new SessionReportResult { Score = 2, MaxPoints = 2, Label = "Level 2", Summary = "Good" });

        var reported = await WaitForStateAsync(sessionId, SessionState.Reported);
        Assert.Equal("Level 2", reported.Result!.Label);
    }

    private async Task<InterviewSession> WaitForStateAsync(string sessionId, SessionState state)
    {
        for (var i = 0; i < 50; i++)
        {
            var session = (await service.GetSessionAsync(sessionId))!;
            if (session.State == state)
                return session;

            await Task.Delay(20);
        }

        throw new TimeoutException($"Session never reached {state}");
    }

    private sealed class FakeEvaluator(Task<SessionReportResult> result) : IReportEvaluator
    {
        public Task<SessionReportResult> EvaluateAsync(
            SkillConfig config, IReadOnlyList<TranscriptEntry> transcript, CancellationToken ct = default) => result;
    }

    private sealed class FakeConfigRepository(SkillConfig config) : ISkillConfigRepository
    {
        public Task<IReadOnlyList<SkillConfig>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<SkillConfig>>([config]);

        public Task<SkillConfig?> GetAsync(string configId) =>
            Task.FromResult(configId == config.Id ? config : null);

        public SkillConfigLoadResult GetLoadResult() => new() { Configs = [config] };
    }
}
