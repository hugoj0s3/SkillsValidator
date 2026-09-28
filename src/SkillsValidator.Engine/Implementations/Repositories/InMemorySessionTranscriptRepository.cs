using System.Collections.Concurrent;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Repositories;

public sealed class InMemorySessionTranscriptRepository : ISessionTranscriptRepository
{
    private readonly ConcurrentDictionary<string, List<TranscriptEntry>> transcripts = new();

    public Task<IReadOnlyList<TranscriptEntry>> GetAsync(string sessionId)
    {
        if (!transcripts.TryGetValue(sessionId, out var entries))
            return Task.FromResult<IReadOnlyList<TranscriptEntry>>([]);

        lock (entries)
            return Task.FromResult<IReadOnlyList<TranscriptEntry>>(entries.ToList());
    }

    public Task AppendAsync(string sessionId, TranscriptEntry entry)
    {
        var entries = transcripts.GetOrAdd(sessionId, _ => []);

        lock (entries)
            entries.Add(entry);

        return Task.CompletedTask;
    }
}
