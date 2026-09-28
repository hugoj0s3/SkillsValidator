using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Implementations;
using SkillsValidator.Engine.Implementations.Realtime;
using SkillsValidator.Engine.Implementations.Repositories;
using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.Models.Realtime;
using SkillsValidator.Web.Realtime;

namespace SkillsValidator.Tests;

/// <summary>
/// Hosts the real voice WebSocket endpoint in memory (fake agent and speech) and talks to it like the browser does.
/// </summary>
public class InterviewVoiceEndpointTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly SkillConfig config = TestConfigs.Create();
    private WebApplication app = null!;
    private TestServer server = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISkillConfigRepository>(new SingleConfigRepository(config));
        builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        builder.Services.AddSingleton<ISessionTranscriptRepository, InMemorySessionTranscriptRepository>();
        builder.Services.AddSingleton<ISessionResultRepository, InMemorySessionResultRepository>();
        builder.Services.AddSingleton<IReportEvaluator, NeverEndingEvaluator>();
        builder.Services.AddSingleton<ISessionInterviewService, SessionInterviewService>();
        builder.Services.AddSingleton<IRealtimeInterviewAgent, ScriptedAgent>();
        builder.Services.AddSingleton<ISpeechConverter, EchoSpeech>();
        builder.Services.AddSingleton<IRealtimeInterviewRunner, RealtimeInterviewRunner>();

        app = builder.Build();
        app.UseWebSockets();
        InterviewVoiceEndpoint.Map(app);

        await app.StartAsync();
        server = app.GetTestServer();
    }

    public async Task DisposeAsync() => await app.DisposeAsync();

    [Fact]
    public async Task Plain_http_request_is_rejected()
    {
        var response = await server.CreateClient().GetAsync("/interview/any/voice");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Full_conversation_follows_the_protocol()
    {
        var sessionId = await app.Services.GetRequiredService<ISessionInterviewService>()
            .StartInterviewSessionAsync("Ana", config);

        using var cts = new CancellationTokenSource(Timeout);
        var socket = await server.CreateWebSocketClient()
            .ConnectAsync(new Uri(server.BaseAddress, $"interview/{sessionId}/voice"), cts.Token);

        // 1. The agent opens the interview: its voice arrives as a binary frame.
        Assert.Equal("audio: Hello Ana.", await ReceiveAsync(socket, cts.Token));

        // 2. The browser sends microphone audio; the fake speech service "hears" a final phrase.
        await socket.SendAsync(new byte[3200], WebSocketMessageType.Binary, endOfMessage: true, cts.Token);

        // 3. Caption cleared, the agent's goodbye, then "ended".
        Assert.Equal("""event: {"type":"caption","text":""}""", await ReceiveAsync(socket, cts.Token));
        Assert.Equal("audio: Goodbye!", await ReceiveAsync(socket, cts.Token));
        Assert.Equal("""event: {"type":"ended"}""", await ReceiveAsync(socket, cts.Token));

        // 4. The browser closes the socket; the server acknowledges.
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
        Assert.Equal(WebSocketState.Closed, socket.State);

        var session = await app.Services.GetRequiredService<ISessionInterviewService>().GetSessionAsync(sessionId);
        Assert.Equal(SessionState.Finished, session!.State);
    }

    private static async Task<string> ReceiveAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var result = await socket.ReceiveAsync(buffer, ct);
        var text = Encoding.UTF8.GetString(buffer, 0, result.Count);

        return result.MessageType == WebSocketMessageType.Binary ? $"audio: {text}" : $"event: {text}";
    }

    /// <summary>Hears one final phrase as soon as the first microphone chunk arrives; "speaks" text as bytes.</summary>
    private sealed class EchoSpeech : ISpeechConverter
    {
        public async IAsyncEnumerable<TranscriptionSegment> ToTranscriptionAsync(
            IAsyncEnumerable<AudioChunk> speech, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var _ in speech.WithCancellation(ct))
            {
                yield return new TranscriptionSegment("I am ready.", IsFinal: true);
            }
        }

        public async IAsyncEnumerable<AudioChunk> ToSpeechAsync(
            string text, VoiceGender gender, VoiceType type, string? speakingStyle,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return new AudioChunk(Encoding.UTF8.GetBytes(text), "text", AudioChunk.AgentSampleRate);
        }
    }

    /// <summary>Greets first, then says goodbye and closes the interview.</summary>
    private sealed class ScriptedAgent : IRealtimeInterviewAgent
    {
        public async IAsyncEnumerable<string> RespondAsync(
            SkillConfig config, InterviewSession session, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return session.Transcriptions.Count == 0 ? "Hello Ana." : "Goodbye! [END]";
        }
    }

    private sealed class NeverEndingEvaluator : IReportEvaluator
    {
        public Task<SessionReportResult> EvaluateAsync(
            SkillConfig config, IReadOnlyList<TranscriptEntry> transcript, CancellationToken ct = default) =>
            new TaskCompletionSource<SessionReportResult>().Task;
    }

    private sealed class SingleConfigRepository(SkillConfig config) : ISkillConfigRepository
    {
        public Task<IReadOnlyList<SkillConfig>> GetAllAsync() =>
            Task.FromResult<IReadOnlyList<SkillConfig>>([config]);

        public Task<SkillConfig?> GetAsync(string configId) =>
            Task.FromResult(configId == config.Id ? config : null);

        public SkillConfigLoadResult GetLoadResult() => new() { Configs = [config] };
    }
}
