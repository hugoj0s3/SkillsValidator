using System.Text.Json;
using System.Text.Json.Serialization;
using SkillsValidator.Engine.Abstractions;
using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Engine.Implementations.Repositories;

/// <summary>
/// Loads every *.json file in the skills folder once, at startup. Configs are read-only.
/// </summary>
public sealed class JsonSkillConfigRepository : ISkillConfigRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SkillConfigLoadResult loadResult;

    public JsonSkillConfigRepository(string skillsFolder, ISkillConfigValidator validator)
    {
        loadResult = Load(skillsFolder, validator);
    }

    public Task<IReadOnlyList<SkillConfig>> GetAllAsync() =>
        Task.FromResult(loadResult.Configs);

    public Task<SkillConfig?> GetAsync(string configId) =>
        Task.FromResult(loadResult.Configs.FirstOrDefault(c => c.Id == configId));

    public SkillConfigLoadResult GetLoadResult() => loadResult;

    private static SkillConfigLoadResult Load(string skillsFolder, ISkillConfigValidator validator)
    {
        if (!Directory.Exists(skillsFolder))
            return new SkillConfigLoadResult();

        var configs = new List<SkillConfig>();
        var errors = new List<ConfigError>();

        foreach (var file in Directory.EnumerateFiles(skillsFolder, "*.json").Order())
        {
            var fileName = Path.GetFileName(file);

            SkillConfig? config;
            try
            {
                config = JsonSerializer.Deserialize<SkillConfig>(File.ReadAllText(file), JsonOptions);
            }
            catch (JsonException ex)
            {
                errors.Add(new ConfigError(fileName, [$"Invalid JSON: {ex.Message}"]));
                continue;
            }

            if (config is null)
            {
                errors.Add(new ConfigError(fileName, ["The file is empty"]));
                continue;
            }

            var messages = validator.Validate(config).ToList();

            if (configs.Any(c => c.Id == config.Id))
                messages.Add($"Id \"{config.Id}\" is already used by another config");

            if (messages.Count > 0)
                errors.Add(new ConfigError(fileName, messages));
            else
                configs.Add(config);
        }

        return new SkillConfigLoadResult { Configs = configs, Errors = errors };
    }
}
