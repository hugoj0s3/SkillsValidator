using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Implementations;
using SkillsValidator.Engine.Implementations.Realtime;
using SkillsValidator.Engine.Implementations.Repositories;
using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.Models.Realtime;

namespace SkillsValidator.Tests;

public class RealtimeInterviewRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly SkillConfig config = TestConfigs.Create();
    private readonly InMemorySessionTranscriptRepository transcripts = new();
    private readonly SessionInterviewService sessions;
    private readonly FakeSpeech speech = new();
    private readonly FakeAgent agent = new();
    private readonly FakeAudioChannel channel = new();
    private readonly RealtimeInterviewRunner runner;

    public RealtimeInterviewRunnerTests()
    {
        sessions = new SessionInterviewService(
            new SingleConfigRepository(config),
            new InMemorySessionRepository(),
            transcripts,
            new InMemorySessionResultRepository(),
            new NeverEndingEvaluator(),
            TimeProvider.System,
            NullLogger<SessionInterviewService>.Instance);

        runner = new RealtimeInterviewRunner(
            sessions, transcripts, agent, speech, TimeProvider.System,
            NullLogger<RealtimeInterviewRunner>.Instance);
    }

    [Fact]
    public async Task Conversation_is_saved_in_order_and_END_finishes_the_session()
    {
        agent.Replies.Add("Hello Ana. What is the Force?");
        agent.Replies.Add("Thanks, Ana. Goodbye! [END]");

        var sessionId = await sessions.StartInterviewSessionAsync("Ana", config);
        var run = runner.RunAsync(sessionId, config, channel, null, CancellationToken.None);

        await WaitUntilAsync(async () => (await transcripts.GetAsync(sessionId)).Count == 1);
        speech.Say("An energy field.", isFinal: true);

        await run.WaitAsync(Timeout);

        var transcript = await transcripts.GetAsync(sessionId);
        Assert.Equal(
            [
                (Speaker.Agent, "Hello Ana. What is the Force?"),
                (Speaker.Participant, "An energy field."),
                (Speaker.Agent, "Thanks, Ana. Goodbye!")
            ],
            transcript.Select(e => (e.Speaker, e.Text)));

        Assert.Equal(SessionState.Finished, (await sessions.GetSessionAsync(sessionId))!.State);
        Assert.DoesNotContain(channel.Played, text => text.Contains("[END]"));
    }

    [Fact]
    public async Task Participant_talking_interrupts_the_agent()
    {
        agent.Replies.Add("Hello Ana. This is a very long introduction. It goes on and on.");
        agent.Replies.Add("Sure, goodbye! [END]");
        channel.BlockPlayback = true; // the first sentence keeps "playing"

        var sessionId = await sessions.StartInterviewSessionAsync("Ana", config);
        var partials = new List<string>();
        var run = runner.RunAsync(sessionId, config, channel, text =>
        {
            partials.Add(text);
            return Task.CompletedTask;
        }, CancellationToken.None);

        await WaitUntilAsync(() => Task.FromResult(channel.Played.Count == 1));
        channel.BlockPlayback = false;

        speech.Say("Can we", isFinal: false);
        await WaitUntilAsync(() => Task.FromResult(channel.StopCount == 1));

        speech.Say("Can we stop?", isFinal: true);
        await run.WaitAsync(Timeout);

        var transcript = await transcripts.GetAsync(sessionId);
        Assert.Equal(
            [
                (Speaker.Agent, "Hello Ana."), // only what was said before the interruption
                (Speaker.Participant, "Can we stop?"),
                (Speaker.Agent, "Sure, goodbye!")
            ],
            transcript.Select(e => (e.Speaker, e.Text)));

        Assert.Contains("Can we", partials);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time");

            await Task.Delay(10);
        }
    }

    /// <summary>Transcription is driven by the test; speech returns the text as bytes.</summary>
    private sealed class FakeSpeech : ISpeechConverter
    {
        private readonly Channel<TranscriptionSegment> segments = Channel.CreateUnbounded<TranscriptionSegment>();

        public void Say(string text, bool isFinal) => segments.Writer.TryWrite(new TranscriptionSegment(text, isFinal));

        public IAsyncEnumerable<TranscriptionSegment> ToTranscriptionAsync(
            IAsyncEnumerable<AudioChunk> speech, CancellationToken ct = default) =>
            segments.Reader.ReadAllAsync(ct);

        public async IAsyncEnumerable<AudioChunk> ToSpeechAsync(
            string text, VoiceGender gender, VoiceType type, string? speakingStyle,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return new AudioChunk(Encoding.UTF8.GetBytes(text), "text", 0);
        }
    }

    /// <summary>Returns the scripted replies in order, word by word, like a streaming LLM.</summary>
    private sealed class FakeAgent : IRealtimeInterviewAgent
    {
        public List<string> Replies { get; } = [];
        private int next;

        public async IAsyncEnumerable<string> RespondAsync(
            SkillConfig config, InterviewSession session, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var reply = Replies[next++];
            foreach (var word in reply.Split(' '))
            {
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                yield return word + " ";
            }
        }
    }

    private sealed class FakeAudioChannel : IAudioChannel
    {
        public List<string> Played { get; } = [];
        public int StopCount { get; private set; }
        public bool BlockPlayback { get; set; }

        // Not used: the fake speech service ignores the audio.
        public async IAsyncEnumerable<AudioChunk> ReadParticipantAudioAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async Task PlayAgentAudioAsync(IAsyncEnumerable<AudioChunk> audio, CancellationToken ct)
        {
            await foreach (var chunk in audio.WithCancellation(ct))
                lock (Played)
                    Played.Add(Encoding.UTF8.GetString(chunk.Data));

            // Simulates a sentence that is still being spoken until the agent is interrupted.
            if (BlockPlayback)
                await Task.Delay(System.Threading.Timeout.Infinite, ct);
        }

        public Task StopAgentAudioAsync()
        {
            StopCount++;
            return Task.CompletedTask;
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
