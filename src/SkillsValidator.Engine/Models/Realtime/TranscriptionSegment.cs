namespace SkillsValidator.Engine.Models.Realtime;

/// <summary>
/// Text produced by speech-to-text while the participant is still speaking.
/// Partial segments (<see cref="IsFinal"/> = false) only update the live UI;
/// final segments are appended to the transcript and sent to the agent.
/// </summary>
public sealed record TranscriptionSegment(string Text, bool IsFinal);
