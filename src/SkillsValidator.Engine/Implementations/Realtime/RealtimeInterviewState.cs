using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Realtime;

/// <summary>
/// The state of one running interview. <see cref="RealtimeInterviewRunner"/> is a singleton,
/// so everything that belongs to a single interview lives here.
/// </summary>
internal sealed class RealtimeInterviewState(
    string sessionId,
    SkillConfig config,
    IAudioChannel channel,
    CancellationTokenSource interviewCts)
{
    public string SessionId { get; } = sessionId;

    public SkillConfig Config { get; } = config;

    public IAudioChannel Channel { get; } = channel;

    /// <summary>Cancelled when the caller stops, or when the agent closes the interview.</summary>
    public CancellationTokenSource InterviewCts { get; } = interviewCts;

    public CancellationToken InterviewToken => InterviewCts.Token;

    /// <summary>The agent's current turn (thinking and speaking).</summary>
    public Task AgentTurn { get; set; } = Task.CompletedTask;

    /// <summary>Cancels the agent's current turn, e.g. when the participant interrupts it.</summary>
    public CancellationTokenSource? AgentTurnCts { get; set; }

    /// <summary>True once the participant interrupted the current agent turn.</summary>
    public bool AgentInterrupted { get; set; }
}
