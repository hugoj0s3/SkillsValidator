using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.OpenAI;

/// <summary>The interviewer, running on an OpenAI chat model through Microsoft.Extensions.AI.</summary>
public sealed class OpenAIInterviewAgent(OpenAIChatClients chatClients, TimeProvider timeProvider) : IRealtimeInterviewAgent
{
    public async IAsyncEnumerable<string> RespondAsync(
        SkillConfig config,
        InterviewSession session,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<ChatMessage> messages = [new(ChatRole.System, InterviewPromptBuilder.Build(config, session))];

        if (session.Transcriptions.Count == 0)
            messages.Add(new ChatMessage(ChatRole.User, "(The participant has joined. Start the interview.)"));

        foreach (var entry in session.Transcriptions)
        {
            var role = entry.Speaker == Speaker.Agent ? ChatRole.Assistant : ChatRole.User;
            messages.Add(new ChatMessage(role, entry.Text));
        }

        // The time note goes last, so the model does not miss it.
        var timeNote = InterviewPromptBuilder.BuildTimeInstruction(config, session, timeProvider.GetUtcNow());
        messages.Add(new ChatMessage(ChatRole.System, timeNote));

        var chatClient = chatClients.Get(config.Effort);
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }
}
