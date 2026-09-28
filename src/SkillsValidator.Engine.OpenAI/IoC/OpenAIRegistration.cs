using Microsoft.Extensions.DependencyInjection;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;

namespace SkillsValidator.Engine.OpenAI.IoC;

public static class OpenAIRegistration
{
    /// <summary>Registers the OpenAI interviewer and report agents, with one model per effort level.</summary>
    public static void AddOpenAI(IServiceCollection services, OpenAIOptions options)
    {
        services.AddSingleton(_ => OpenAIChatClients.Create(options));

        services.AddSingleton<IRealtimeInterviewAgent, OpenAIInterviewAgent>();
        services.AddSingleton<IReportEvaluator, OpenAIReportEvaluator>();
    }
}
