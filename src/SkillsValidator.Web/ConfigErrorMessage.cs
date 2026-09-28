using System.Text;
using SkillsValidator.Engine.Models;

namespace SkillsValidator.Web;

/// <summary>Builds the "fix your config and re-run" message shown on the error page and in the console.</summary>
public static class ConfigErrorMessage
{
    public static string Build(SkillConfigLoadResult loadResult, string skillsFolder)
    {
        if (loadResult.NoConfigFound)
            return $"No skill config found in {skillsFolder}. Add at least one .json config.";

        var message = new StringBuilder();
        foreach (var error in loadResult.Errors)
        {
            message.AppendLine($"Config \"{error.FileName}\" is invalid:");
            foreach (var line in error.Messages)
                message.AppendLine($"  - {line}");
        }

        return message.ToString().TrimEnd();
    }
}
