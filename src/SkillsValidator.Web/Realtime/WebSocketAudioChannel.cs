using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Models.Realtime;

namespace SkillsValidator.Web.Realtime;

/// <summary>
/// The audio pipe to the participant's browser over one WebSocket (see <see cref="InterviewVoiceEndpoint"/>
/// for the protocol). Binary frames carry audio, text frames carry small JSON events.
/// </summary>
public sealed class WebSocketAudioChannel(WebSocket socket) : IAudioChannel
{
    // If the server falls behind, the oldest microphone audio is dropped instead of piling up.
    private readonly Channel<AudioChunk> microphone = Channel.CreateBounded<AudioChunk>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

    // WebSocket allows only one send at a time; the agent audio and the events are sent from different tasks.
    private readonly SemaphoreSlim sendLock = new(1, 1);

    /// <summary>
    /// Reads the microphone audio frames sent by the browser until it closes the socket.
    /// </summary>
    public async Task ReceiveMicrophoneAsync(CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var frame = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                // A frame can arrive in several parts; collect them until the end of the message.
                frame.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                    continue;

                if (result.MessageType == WebSocketMessageType.Binary)
                    microphone.Writer.TryWrite(new AudioChunk(frame.ToArray(), "audio/pcm", AudioChunk.MicrophoneSampleRate));

                frame.SetLength(0);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // The interview stopped, or the browser disconnected without closing properly.
        }
        finally
        {
            microphone.Writer.TryComplete();
        }
    }

    /// <summary>Acknowledges the browser's close, which completes the WebSocket closing handshake.</summary>
    public async Task CloseAsync()
    {
        await sendLock.WaitAsync();
        try
        {
            if (socket.State == WebSocketState.CloseReceived)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // The browser is already gone.
        }
        finally
        {
            sendLock.Release();
        }
    }

    public IAsyncEnumerable<AudioChunk> ReadParticipantAudioAsync(CancellationToken ct) =>
        microphone.Reader.ReadAllAsync(ct);

    public async Task PlayAgentAudioAsync(IAsyncEnumerable<AudioChunk> audio, CancellationToken ct)
    {
        await foreach (var chunk in audio.WithCancellation(ct))
            await SendAsync(chunk.Data, WebSocketMessageType.Binary, ct);
    }

    public Task StopAgentAudioAsync() => SendEventAsync(new { type = "stop" });

    /// <summary>What the participant is saying right now (an empty text clears the caption).</summary>
    public Task SendCaptionAsync(string text) => SendEventAsync(new { type = "caption", text });

    /// <summary>The agent closed the interview.</summary>
    public Task SendEndedAsync() => SendEventAsync(new { type = "ended" });

    /// <summary>The voice connection failed; the browser shows the message.</summary>
    public Task SendErrorAsync(string message) => SendEventAsync(new { type = "error", message });

    private Task SendEventAsync(object message) =>
        SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, CancellationToken.None);

    private async Task SendAsync(byte[] data, WebSocketMessageType type, CancellationToken ct)
    {
        await sendLock.WaitAsync(ct);
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.SendAsync(data, type, endOfMessage: true, ct);
        }
        catch (WebSocketException)
        {
            // The browser is gone; ReceiveMicrophoneAsync notices it and the interview stops.
        }
        finally
        {
            sendLock.Release();
        }
    }
}
