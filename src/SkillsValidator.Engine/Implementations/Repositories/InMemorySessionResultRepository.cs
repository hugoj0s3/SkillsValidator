using System.Collections.Concurrent;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Repositories;

public sealed class InMemorySessionResultRepository : ISessionResultRepository
{
    private readonly ConcurrentDictionary<string, SessionReportResult> results = new();

    public Task<SessionReportResult?> GetAsync(string sessionId) =>
        Task.FromResult(results.GetValueOrDefault(sessionId));

    public Task InsertAsync(string sessionId, SessionReportResult result)
    {
        if (!results.TryAdd(sessionId, result))
            throw new InvalidOperationException($"Session {sessionId} already has a result");

        return Task.CompletedTask;
    }
}
