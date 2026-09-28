using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Models;
using SkillsValidator.Engine.Models.Realtime;

namespace SkillsValidator.Engine.AzureSpeech;

/// <summary>Speech-to-text and text-to-speech with the Azure Speech SDK.</summary>
public sealed class AzureSpeechConverter(AzureSpeechOptions options) : ISpeechConverter, IDisposable
{
    private readonly ConcurrentDictionary<string, SpeechSynthesizer> synthesizers = new();

    public async IAsyncEnumerable<TranscriptionSegment> ToTranscriptionAsync(
        IAsyncEnumerable<AudioChunk> speech,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var speechConfig = SpeechConfig.FromSubscription(options.Key, options.Region);
        speechConfig.SpeechRecognitionLanguage = options.Language;
        // Azure ends a phrase (final result) after this much silence: this is our turn detection.
        speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, options.SilenceTimeoutMs.ToString());

        using var pushStream = AudioInputStream.CreatePushStream(
            AudioStreamFormat.GetWaveFormatPCM(AudioChunk.MicrophoneSampleRate, 16, 1));
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        // Bridge the recognizer events to an async stream.
        var segments = Channel.CreateUnbounded<TranscriptionSegment>();

        recognizer.Recognizing += (_, e) =>
            segments.Writer.TryWrite(new TranscriptionSegment(e.Result.Text, IsFinal: false));

        recognizer.Recognized += (_, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                segments.Writer.TryWrite(new TranscriptionSegment(e.Result.Text, IsFinal: true));
        };

        recognizer.Canceled += (_, e) => segments.Writer.TryComplete(
            e.Reason == CancellationReason.Error
                ? new InvalidOperationException($"Azure Speech error {e.ErrorCode}: {e.ErrorDetails}")
                : null);

        recognizer.SessionStopped += (_, _) => segments.Writer.TryComplete();

        await recognizer.StartContinuousRecognitionAsync();

        // Feed the microphone audio in the background while the segments are read.
        using var feedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var feeding = FeedAsync(speech, pushStream, feedCts.Token);

        try
        {
            await foreach (var segment in segments.Reader.ReadAllAsync(ct))
                yield return segment;
        }
        finally
        {
            await feedCts.CancelAsync();
            await feeding;
            await recognizer.StopContinuousRecognitionAsync();
        }
    }

    public async IAsyncEnumerable<AudioChunk> ToSpeechAsync(
        string text,
        VoiceGender gender,
        VoiceType type,
        string? speakingStyle,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var synthesizer = GetSynthesizer(options.GetVoiceName(gender, type));
        using var result = await synthesizer.SpeakTextAsync(text);

        if (result.Reason == ResultReason.Canceled)
        {
            var details = SpeechSynthesisCancellationDetails.FromResult(result);
            throw new InvalidOperationException($"Azure Speech synthesis error {details.ErrorCode}: {details.ErrorDetails}");
        }

        yield return new AudioChunk(result.AudioData, "audio/pcm", AudioChunk.AgentSampleRate);
    }

    /// <summary>
    /// One synthesizer per voice, created once and reused: it keeps its connection to Azure open,
    /// which saves about a second per sentence compared to connecting every time.
    /// </summary>
    private SpeechSynthesizer GetSynthesizer(string voiceName) =>
        synthesizers.GetOrAdd(voiceName, CreateSynthesizer);

    private SpeechSynthesizer CreateSynthesizer(string voiceName)
    {
        var speechConfig = SpeechConfig.FromSubscription(options.Key, options.Region);
        speechConfig.SpeechSynthesisVoiceName = voiceName;
        // Raw 16-bit mono PCM at 24 kHz: the browser plays it without decoding.
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);

        // A null AudioConfig returns the audio data instead of playing it on the server's speaker.
        return new SpeechSynthesizer(speechConfig, null as AudioConfig);
    }

    public void Dispose()
    {
        foreach (var synthesizer in synthesizers.Values)
            synthesizer.Dispose();
    }

    private static async Task FeedAsync(IAsyncEnumerable<AudioChunk> speech, PushAudioInputStream pushStream, CancellationToken ct)
    {
        try
        {
            await foreach (var chunk in speech.WithCancellation(ct))
                pushStream.Write(chunk.Data);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // Signals the end of the audio to the recognizer.
            pushStream.Close();
        }
    }
}
