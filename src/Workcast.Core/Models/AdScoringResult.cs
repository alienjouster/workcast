namespace Workcast.Core.Models;

/// <summary>
/// The structured result returned by the AI provider after scoring a resume
/// against a job posting. Consumed by <c>AdScoringJob</c> to create an
/// <see cref="Workcast.Core.Entities.AdScoring"/> entity.
/// </summary>
public sealed class AdScoringResult
{
    /// <summary>Overall match score 0–100, averaged from all requirement scores.</summary>
    public double OverallScore { get; set; }

    /// <summary>Short narrative summary of the match quality.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Per-requirement analysis produced by the AI.</summary>
    public List<AdScoringRequirementResult> Requirements { get; set; } = [];
}

/// <summary>A single requirement result from the AI scoring tool call.</summary>
public sealed class AdScoringRequirementResult
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "gap";
    public bool IsOptional { get; set; }
    public double Score { get; set; }
    public string? Notes { get; set; }
}
