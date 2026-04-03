namespace Workcast.Core.Models;

/// <summary>
/// The structured output returned by the AI provider for interview drill generation.
/// </summary>
public sealed class InterviewDrillResult
{
    public List<InterviewQuestionResult> Questions { get; set; } = [];
}

/// <summary>A single interview question as returned by the AI provider.</summary>
public sealed class InterviewQuestionResult
{
    public int OrderIndex { get; set; }
    public string Text { get; set; } = "";

    /// <summary>warm_up | easy | medium | challenging</summary>
    public string Category { get; set; } = "";

    public string? RequirementName { get; set; }
}
