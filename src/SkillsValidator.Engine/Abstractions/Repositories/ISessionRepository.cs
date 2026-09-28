using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.Repositories;

/// <summary>Stores the session itself (state, duration, participant). Kept in memory.</summary>
public interface ISessionRepository
{
    Task<InterviewSession?> GetAsync(string sessionId);

    Task InsertAsync(InterviewSession session);

    Task UpdateAsync(InterviewSession session);
}
