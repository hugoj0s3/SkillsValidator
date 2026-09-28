namespace SkillsValidator.Engine.Models;

/// <summary>
/// The complete view of an interview session: its state, transcript and (once evaluated) result.
/// </summary>
public sealed class InterviewSession
{
    public required string Id { get; init; }

    public required string ConfigId { get; init; }

    public required string ParticipantName { get; init; }

    public SessionState State { get; set; }

    public DateTimeOffset StartedAt { get; init; }

    public TimeSpan Duration { get; set; }

    public IList<TranscriptEntry> Transcriptions { get; set; } = [];

    /// <summary>Null until <see cref="State"/> is <see cref="SessionState.Reported"/>.</summary>
    public SessionReportResult? Result { get; set; }
}
