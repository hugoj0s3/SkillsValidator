namespace SkillsValidator.Engine.Models;

/// <summary>A single spoken turn in the interview. Only the text is stored, never the audio.</summary>
public sealed record TranscriptEntry(Speaker Speaker, string Text, DateTimeOffset Timestamp);
