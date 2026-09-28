using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Web.Realtime;

/// <summary>
/// The voice of an interview travels over one WebSocket: <c>/interview/{sessionId}/voice</c>.
/// <list type="bullet">
///   <item>browser → server, binary: microphone audio, 16-bit mono PCM at 16 kHz (~100 ms per frame)</item>
///   <item>server → browser, binary: agent voice, 16-bit mono PCM at 24 kHz (one sentence per frame)</item>
///   <item>server → browser, text: {"type":"caption","text":"..."}: what the participant is saying</item>
///   <item>server → browser, text: {"type":"stop"}: the participant interrupted, stop the agent audio</item>
///   <item>server → browser, text: {"type":"ended"}: the agent closed the interview</item>
///   <item>server → browser, text: {"type":"error","message":"..."}: the voice connection failed</item>
/// </list>
/// The browser always closes the socket (after the goodbye, on an error, or when leaving the page);
/// that also stops the interview loop.
/// </summary>
public static class InterviewVoiceEndpoint
{
    public static void Map(WebApplication app) =>
        app.Map("/interview/{sessionId}/voice", HandleAsync);

    private static async Task HandleAsync(
        HttpContext context,
        string sessionId,
        ISessionInterviewService sessionService,
        ISkillConfigRepository configRepository,
        IRealtimeInterviewRunner runner,
        ILogger<WebSocketAudioChannel> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var session = await sessionService.GetSessionAsync(sessionId);
        var config = session is null ? null : await configRepository.GetAsync(session.ConfigId);
        if (session is not { State: SessionState.Running } || config is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var channel = new WebSocketAudioChannel(socket);

        // Cancelled when the browser closes the socket or the connection drops.
        using var interviewCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var receiving = ReceiveUntilClosedAsync(channel, interviewCts);

        try
        {
            await runner.RunAsync(sessionId, config, channel, channel.SendCaptionAsync, interviewCts.Token);

            // The runner also returns when the agent says goodbye: tell the browser.
            if (!interviewCts.IsCancellationRequested)
                await channel.SendEndedAsync();
        }
        catch (Exception ex) when (!interviewCts.IsCancellationRequested)
        {
            logger.LogError(ex, "Voice interview failed for session {SessionId}", sessionId);
            await channel.SendErrorAsync($"The voice connection failed: {ex.Message}");
        }

        // Wait for the browser to close the socket, then acknowledge it.
        await receiving;
        await channel.CloseAsync();
    }

    private static async Task ReceiveUntilClosedAsync(WebSocketAudioChannel channel, CancellationTokenSource interviewCts)
    {
        await channel.ReceiveMicrophoneAsync(interviewCts.Token);
        await interviewCts.CancelAsync();
    }
}
