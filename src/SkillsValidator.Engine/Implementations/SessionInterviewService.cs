using Microsoft.Extensions.Logging;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations;

public sealed class SessionInterviewService(
    ISkillConfigRepository configRepository,
    ISessionRepository sessionRepository,
    ISessionTranscriptRepository transcriptRepository,
    ISessionResultRepository resultRepository,
    IReportEvaluator reportEvaluator,
    TimeProvider timeProvider,
    ILogger<SessionInterviewService> logger) : ISessionInterviewService
{
    public async Task<string> StartInterviewSessionAsync(string participantName, SkillConfig config)
    {
        var session = new InterviewSession
        {
            Id = Guid.NewGuid().ToString("N"),
            ConfigId = config.Id,
            ParticipantName = participantName.Trim(),
            State = SessionState.Running,
            StartedAt = timeProvider.GetUtcNow()
        };

        await sessionRepository.InsertAsync(session);
        return session.Id;
    }

    public async Task FinishInterviewSessionAsync(string sessionId)
    {
        var session = await sessionRepository.GetAsync(sessionId);
        if (session is null || session.State != SessionState.Running)
            return;

        session.State = SessionState.Finished;
        session.Duration = timeProvider.GetUtcNow() - session.StartedAt;
        await sessionRepository.UpdateAsync(session);

        // The evaluation can take a while, so it runs in the background.
        // The UI polls GetSessionAsync until the state is Reported.
        _ = Task.Run(() => EvaluateAsync(session));
    }

    public async Task<InterviewSession?> GetSessionAsync(string sessionId)
    {
        var session = await sessionRepository.GetAsync(sessionId);
        if (session is null)
            return null;

        return new InterviewSession
        {
            Id = session.Id,
            ConfigId = session.ConfigId,
            ParticipantName = session.ParticipantName,
            State = session.State,
            StartedAt = session.StartedAt,
            Duration = session.State == SessionState.Running
                ? timeProvider.GetUtcNow() - session.StartedAt
                : session.Duration,
            Transcriptions = (await transcriptRepository.GetAsync(sessionId)).ToList(),
            Result = await resultRepository.GetAsync(sessionId)
        };
    }

    private async Task EvaluateAsync(InterviewSession session)
    {
        try
        {
            var config = await configRepository.GetAsync(session.ConfigId)
                         ?? throw new InvalidOperationException($"Config {session.ConfigId} not found");
            var transcript = await transcriptRepository.GetAsync(session.Id);

            var result = await reportEvaluator.EvaluateAsync(config, transcript);

            await resultRepository.InsertAsync(session.Id, result);
            session.State = SessionState.Reported;
            await sessionRepository.UpdateAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation of session {SessionId} failed", session.Id);
        }
    }
}
