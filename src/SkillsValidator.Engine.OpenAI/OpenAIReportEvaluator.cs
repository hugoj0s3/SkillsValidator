using Microsoft.Extensions.AI;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.OpenAI;

/// <summary>The report agent, running on an OpenAI chat model with structured (JSON) output.</summary>
public sealed class OpenAIReportEvaluator(OpenAIChatClients chatClients) : IReportEvaluator
{
    public async Task<SessionReportResult> EvaluateAsync(
        SkillConfig config,
        IReadOnlyList<TranscriptEntry> transcript,
        CancellationToken ct = default)
    {
        if (!transcript.Any(e => e.Speaker == Speaker.Participant))
            return ReportPromptBuilder.NoAnswers(config);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, ReportPromptBuilder.BuildSystemPrompt(config)),
            new(ChatRole.User, $"Transcript:\n{ReportPromptBuilder.BuildTranscript(transcript)}")
        ];

        // Asks the model for JSON matching ReportResponse.
        var chatClient = chatClients.Get(config.Effort);
        var response = await chatClient.GetResponseAsync<ReportResponse>(messages, cancellationToken: ct);

        var report = response.Result;
        return ReportPromptBuilder.ToResult(config, report.Score, report.Summary, report.StrongPoints, report.ImprovementAreas);
    }

    /// <summary>The JSON shape the model must return.</summary>
    private sealed record ReportResponse(
        int Score,
        string Summary,
        List<string> StrongPoints,
        List<string> ImprovementAreas);
}
