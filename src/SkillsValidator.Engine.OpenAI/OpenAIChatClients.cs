using Microsoft.Extensions.AI;
using OpenAI;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.OpenAI;

/// <summary>One chat client per effort level; the agents pick the one for the skill's <see cref="SkillConfig.Effort"/>.</summary>
public sealed class OpenAIChatClients(IReadOnlyDictionary<ModelEffort, IChatClient> clients)
{
    public IChatClient Get(ModelEffort effort) => clients[effort];

    /// <summary>Creates a client for each model configured in <see cref="OpenAIOptions.Models"/>.</summary>
    public static OpenAIChatClients Create(OpenAIOptions options)
    {
        var openAIClient = new OpenAIClient(options.ApiKey);

        var clients = options.Models.ToDictionary(
            pair => pair.Key,
            pair => openAIClient.GetChatClient(pair.Value).AsIChatClient());

        return new OpenAIChatClients(clients);
    }
}
