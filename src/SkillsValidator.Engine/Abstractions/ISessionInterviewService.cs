using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Abstractions;

/// <summary>Entry point for starting, finishing and reading interview sessions.</summary>
public interface ISessionInterviewService
{
    /// <summary>Creates a new running session and returns its id.</summary>
    Task<string> StartInterviewSessionAsync(string participantName, SkillConfig config);

    /// <summary>Moves the session from Running to Finished and triggers the evaluation.</summary>
    Task FinishInterviewSessionAsync(string sessionId);

    /// <summary>
    /// Returns the complete session (transcriptions and result included), or null if it does not exist.
    /// State Finished means the evaluation is pending; Reported means <see cref="InterviewSession.Result"/> is filled.
    /// </summary>
    Task<InterviewSession?> GetSessionAsync(string sessionId);
}
