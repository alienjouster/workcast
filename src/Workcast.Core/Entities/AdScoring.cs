namespace Workcast.Core.Entities;

/// <summary>
/// Stores the result of an AI-powered scoring analysis that compares a user's resume
/// against a specific job ad. Only one scoring record exists per job ad at any time;
/// re-running scoring replaces the previous result.
/// </summary>
public sealed class AdScoring
{
    private AdScoring() { }

    /// <summary>Creates a new scoring record for a job ad.</summary>
    public static AdScoring Create(
        Guid jobAdId,
        double overallScore,
        string summary,
        string recommendation,
        List<ScoringRequirement> requirements)
    {
        return new AdScoring
        {
            JobAdId = jobAdId,
            ScoredAt = DateTimeOffset.UtcNow,
            OverallScore = overallScore,
            Summary = summary,
            Recommendation = recommendation,
            Requirements = requirements,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Foreign key to the scored <see cref="JobAd"/>.</summary>
    public Guid JobAdId { get; private set; }

    /// <summary>Navigation property — the scored job ad.</summary>
    public JobAd JobAd { get; private set; } = null!;

    /// <summary>UTC timestamp when scoring was performed.</summary>
    public DateTimeOffset ScoredAt { get; private set; }

    /// <summary>Overall match score from 0 to 100, averaged from per-requirement scores.</summary>
    public double OverallScore { get; private set; }

    /// <summary>Short narrative summary produced by the AI.</summary>
    public string Summary { get; private set; } = "";

    /// <summary>Actionable recommendation on how the candidate could improve their match.</summary>
    public string Recommendation { get; private set; } = "";

    /// <summary>
    /// Per-requirement breakdown stored as a JSONB column.
    /// Each entry describes one skill, qualification, or requirement from the job posting.
    /// </summary>
    public List<ScoringRequirement> Requirements { get; private set; } = [];
}

/// <summary>
/// A single requirement extracted from a job posting and compared against the user's resume.
/// Stored in the <see cref="AdScoring.Requirements"/> JSONB column.
/// </summary>
public sealed class ScoringRequirement
{
    /// <summary>Name of the skill or requirement (e.g., "React", "5 years experience").</summary>
    public string Name { get; set; } = "";

    /// <summary>Match result: "match", "partial_match", or "gap".</summary>
    public string Category { get; set; } = "gap";

    /// <summary>True when the posting marks this requirement as optional or "nice to have".</summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Score for this requirement (0–100).
    /// Typically 100 for match, 50 for partial match, 0 for gap.
    /// Optional gaps/partial matches may receive adjusted scores.
    /// </summary>
    public double Score { get; set; }

    /// <summary>Brief explanation of the AI's reasoning.</summary>
    public string? Notes { get; set; }
}
