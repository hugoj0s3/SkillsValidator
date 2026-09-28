using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.Repositories;

/// <summary>The evaluation result of a session. Kept in memory.</summary>
public interface ISessionResultRepository
{
    Task<SessionReportResult?> GetAsync(string sessionId);

    Task InsertAsync(string sessionId, SessionReportResult result);
}
