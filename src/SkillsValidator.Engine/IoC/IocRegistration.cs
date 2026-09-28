using Microsoft.Extensions.DependencyInjection;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.ArtificialIntelligence;
using SkillsValidator.Engine.Abstractions.Realtime;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Implementations;
using SkillsValidator.Engine.Implementations.Realtime;
using SkillsValidator.Engine.Implementations.Repositories;

namespace SkillsValidator.Engine.IoC;

public static class IocRegistration
{
    /// <summary>
    /// Registers the JSON config repository, the in-memory session storage, the session service
    /// and the realtime runner.
    /// The providers must be registered separately: an <see cref="IRealtimeInterviewAgent"/> and an
    /// <see cref="IReportEvaluator"/> (e.g. SkillsValidator.Engine.OpenAI), and an <see cref="ISpeechConverter"/>
    /// (e.g. SkillsValidator.Engine.AzureSpeech).
    /// </summary>
    public static void AddSkillsValidator(IServiceCollection services, string skillsFolder)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISkillConfigValidator, SkillConfigValidator>();
        services.AddSingleton<ISkillConfigRepository>(sp =>
            new JsonSkillConfigRepository(skillsFolder, sp.GetRequiredService<ISkillConfigValidator>()));

        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.AddSingleton<ISessionTranscriptRepository, InMemorySessionTranscriptRepository>();
        services.AddSingleton<ISessionResultRepository, InMemorySessionResultRepository>();
        services.AddSingleton<ISessionInterviewService, SessionInterviewService>();

        services.AddSingleton<IRealtimeInterviewRunner, RealtimeInterviewRunner>();
    }
}
