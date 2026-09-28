using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Realtime;

public sealed class RealtimeInterviewRunner(
    ISessionInterviewService sessionService,
    ISessionTranscriptRepository transcriptRepository,
    IRealtimeInterviewAgent agent,
    ISpeechConverter speech,
    TimeProvider timeProvider,
    ILogger<RealtimeInterviewRunner> logger) : IRealtimeInterviewRunner
{
    public async Task RunAsync(
        string sessionId,
        SkillConfig config,
        IAudioChannel channel,
        Func<string, Task>? onPartialTranscript,
        CancellationToken ct)
    {
        using var interviewCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var state = new RealtimeInterviewState(sessionId, config, channel, interviewCts);

        // The agent opens the interview.
        StartAgentTurn(state);

        try
        {
            var participantAudio = channel.ReadParticipantAudioAsync(state.InterviewToken);
            var segments = speech.ToTranscriptionAsync(participantAudio, state.InterviewToken);

            await foreach (var segment in segments.WithCancellation(state.InterviewToken))
            {
                if (segment.IsFinal)
                    await OnParticipantFinishedAsync(state, segment.Text, onPartialTranscript);
                else
                    await OnParticipantSpeakingAsync(state, segment.Text, onPartialTranscript);
            }
        }
        catch (OperationCanceledException) when (state.InterviewToken.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAgentTurnAsync(state);
            state.AgentTurnCts?.Dispose();
        }
    }

    /// <summary>The participant is speaking: show the live caption and interrupt the agent (barge-in).</summary>
    private async Task OnParticipantSpeakingAsync(
        RealtimeInterviewState state,
        string partialText,
        Func<string, Task>? onPartialTranscript)
    {
        if (onPartialTranscript is not null)
            await onPartialTranscript(partialText);

        if (state.AgentInterrupted)
            return;

        state.AgentInterrupted = true;
        await state.AgentTurnCts!.CancelAsync();
        await state.Channel.StopAgentAudioAsync();
    }

    /// <summary>
    /// The participant finished a phrase: wait for the current agent turn to stop (so the transcript
    /// stays in order), save the phrase and let the agent answer.
    /// </summary>
    private async Task OnParticipantFinishedAsync(
        RealtimeInterviewState state,
        string text,
        Func<string, Task>? onPartialTranscript)
    {
        await StopAgentTurnAsync(state);

        if (onPartialTranscript is not null)
            await onPartialTranscript("");

        await transcriptRepository.AppendAsync(
            state.SessionId,
            new TranscriptEntry(Speaker.Participant, text, timeProvider.GetUtcNow()));

        StartAgentTurn(state);
    }

    /// <summary>Starts the agent's next turn in the background, so listening continues meanwhile.</summary>
    private void StartAgentTurn(RealtimeInterviewState state)
    {
        state.AgentTurnCts?.Dispose();
        state.AgentTurnCts = CancellationTokenSource.CreateLinkedTokenSource(state.InterviewToken);
        state.AgentInterrupted = false;
        state.AgentTurn = RunAgentTurnAsync(state, state.AgentTurnCts.Token);
    }

    /// <summary>Cancels the agent's current turn and waits until it has stopped.</summary>
    private static async Task StopAgentTurnAsync(RealtimeInterviewState state)
    {
        if (state.AgentTurnCts is not null)
            await state.AgentTurnCts.CancelAsync();

        await state.AgentTurn;
    }

    /// <summary>Runs one agent turn; when the agent closes the interview, finishes the session.</summary>
    private async Task RunAgentTurnAsync(RealtimeInterviewState state, CancellationToken ct)
    {
        try
        {
            var closed = await SpeakAgentTurnAsync(state, ct);
            if (closed)
            {
                await sessionService.FinishInterviewSessionAsync(state.SessionId);
                await state.InterviewCts.CancelAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Agent turn failed for session {SessionId}", state.SessionId);
        }
    }

    /// <summary>
    /// Streams the agent's reply and speaks it sentence by sentence.
    /// Returns true when the agent closed the interview.
    /// </summary>
    private async Task<bool> SpeakAgentTurnAsync(RealtimeInterviewState state, CancellationToken ct)
    {
        var session = await sessionService.GetSessionAsync(state.SessionId)
                      ?? throw new InvalidOperationException($"Session {state.SessionId} not found");

        var splitter = new SentenceSplitter();
        var spoken = new StringBuilder();
        var closed = false;

        // Measures how long the participant waits for the agent to start speaking.
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await foreach (var fragment in agent.RespondAsync(state.Config, session, ct))
            {
                foreach (var sentence in splitter.Append(fragment))
                    closed |= await SpeakSentenceAsync(state, sentence, spoken, stopwatch, ct);
            }

            if (splitter.Flush() is { } rest)
                closed |= await SpeakSentenceAsync(state, rest, spoken, stopwatch, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Interrupted by the participant: keep what was already said.
        }
        finally
        {
            if (spoken.Length > 0)
            {
                await transcriptRepository.AppendAsync(
                    state.SessionId,
                    new TranscriptEntry(Speaker.Agent, spoken.ToString().Trim(), timeProvider.GetUtcNow()));
            }
        }

        return closed;
    }

    /// <summary>
    /// Speaks one sentence (without the end marker) and adds it to <paramref name="spoken"/>.
    /// Returns true when the sentence contains the end marker, i.e. the agent closes the interview.
    /// </summary>
    private async Task<bool> SpeakSentenceAsync(
        RealtimeInterviewState state,
        string sentence,
        StringBuilder spoken,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        var closesInterview = sentence.Contains(InterviewBaseInstructions.EndMarker);

        var text = sentence.Replace(InterviewBaseInstructions.EndMarker, "").Trim();
        if (text.Length == 0)
            return closesInterview;

        var isFirstSentence = spoken.Length == 0;
        var textReadyMs = stopwatch.ElapsedMilliseconds;
        spoken.Append(text).Append(' ');

        var config = state.Config;
        var audio = speech.ToSpeechAsync(text, config.AgentVoiceGender, config.AgentVoiceType, config.AgentTone, ct);
        await state.Channel.PlayAgentAudioAsync(audio, ct);

        if (isFirstSentence)
        {
            logger.LogInformation(
                "Agent turn: first sentence after {SentenceMs} ms, its audio sent after {AudioMs} ms",
                textReadyMs, stopwatch.ElapsedMilliseconds);
        }

        return closesInterview;
    }
}
