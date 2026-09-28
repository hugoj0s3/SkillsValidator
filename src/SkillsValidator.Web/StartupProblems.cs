namespace SkillsValidator.Web;

/// <summary>
/// Problems found at startup (invalid skill configs, missing keys). When there are any,
/// the app shows only the error page: fix them and re-run.
/// </summary>
public sealed class StartupProblems
{
    public List<string> Messages { get; } = [];

    public bool Any => Messages.Count > 0;
}
