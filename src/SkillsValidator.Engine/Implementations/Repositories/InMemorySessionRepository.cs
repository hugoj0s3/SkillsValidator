using System.Collections.Concurrent;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, InterviewSession> sessions = new();

    public Task<InterviewSession?> GetAsync(string sessionId) =>
        Task.FromResult(sessions.GetValueOrDefault(sessionId));

    public Task InsertAsync(InterviewSession session)
    {
        if (!sessions.TryAdd(session.Id, session))
            throw new InvalidOperationException($"Session {session.Id} already exists");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(InterviewSession session)
    {
        sessions[session.Id] = session;
        return Task.CompletedTask;
    }
}
