namespace Workcast.Core.Entities;

/// <summary>
/// Stores a generated interview drill plan for an application.
/// Exactly one plan exists per application — regenerating replaces the previous plan.
/// Questions are stored as a JSONB array.
/// </summary>
public sealed class InterviewDrillPlan
{
    private InterviewDrillPlan() { }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Reference to the application this drill plan belongs to.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>UTC timestamp when the plan was generated.</summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>Anthropic model identifier used to generate this plan.</summary>
    public string ModelUsed { get; private set; } = "";

    /// <summary>Ordered list of interview questions. Stored as JSONB.</summary>
    public List<InterviewQuestion> Questions { get; private set; } = [];

    /// <summary>Creates a new interview drill plan for the given application.</summary>
    public static InterviewDrillPlan Create(
        Guid applicationId,
        string modelUsed,
        List<InterviewQuestion> questions)
    {
        return new InterviewDrillPlan
        {
            ApplicationId = applicationId,
            GeneratedAt   = DateTimeOffset.UtcNow,
            ModelUsed     = modelUsed,
            Questions     = questions,
        };
    }
}

/// <summary>A single interview question within a drill plan.</summary>
public sealed class InterviewQuestion
{
    /// <summary>1-based display order.</summary>
    public int OrderIndex { get; set; }

    /// <summary>The question text.</summary>
    public string Text { get; set; } = "";

    /// <summary>Difficulty category: warm_up | easy | medium | challenging.</summary>
    public string Category { get; set; } = "";

    /// <summary>The scoring requirement that inspired this question, or null for warm-up and job-ad-derived questions.</summary>
    public string? RequirementName { get; set; }

    /// <summary>The user's answer to this question, recorded during a drill session. Null if not yet answered.</summary>
    public string? Answer { get; set; }

    /// <summary>UTC timestamp when the answer was last saved. Null if not yet answered.</summary>
    public DateTimeOffset? AnsweredAt { get; set; }
}
