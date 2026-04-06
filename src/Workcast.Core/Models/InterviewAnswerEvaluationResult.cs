namespace Workcast.Core.Models;

/// <summary>
/// The structured output returned by the AI provider for interview answer evaluation.
/// </summary>
public sealed class InterviewAnswerEvaluationResult
{
    /// <summary>Overall quality rating: "good", "satisfactory", or "needs_improvement".</summary>
    public string Rating { get; init; } = "";

    /// <summary>2–3 sentence recruiter-perspective assessment of the answer.</summary>
    public string Feedback { get; init; } = "";

    /// <summary>Actionable improvement tips (what to say, what to avoid, how to frame gaps).</summary>
    public List<string> Tips { get; init; } = [];
}
