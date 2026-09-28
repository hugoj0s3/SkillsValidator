namespace SkillsValidator.Engine.Models;

public enum SessionState
{
    /// <summary>The interview is in progress.</summary>
    Running,

    /// <summary>The interview is over and the evaluation is pending.</summary>
    Finished,

    /// <summary>The evaluation is done and <see cref="InterviewSession.Result"/> is filled.</summary>
    Reported
}
