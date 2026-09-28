using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions.Repositories;

/// <summary>Append-only transcript of a session. Kept in memory.</summary>
public interface ISessionTranscriptRepository
{
    Task<IReadOnlyList<TranscriptEntry>> GetAsync(string sessionId);

    Task AppendAsync(string sessionId, TranscriptEntry entry);
}
