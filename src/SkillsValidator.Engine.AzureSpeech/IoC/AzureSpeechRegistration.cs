using Microsoft.Extensions.DependencyInjection;
using SkillsValidator.Engine.Abstractions.Realtime;

namespace SkillsValidator.Engine.AzureSpeech.IoC;

public static class AzureSpeechRegistration
{
    /// <summary>Registers Azure Speech as the <see cref="ISpeechConverter"/> (speech-to-text and text-to-speech).</summary>
    public static void AddAzureSpeech(IServiceCollection services, AzureSpeechOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ISpeechConverter, AzureSpeechConverter>();
    }
}
