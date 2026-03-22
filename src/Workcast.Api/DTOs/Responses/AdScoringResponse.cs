namespace Workcast.Api.DTOs.Responses;

/// <summary>API response for a job ad scoring result.</summary>
public sealed class AdScoringResponse
{
    public Guid Id { get; init; }
    public Guid JobAdId { get; init; }
    public DateTimeOffset ScoredAt { get; init; }
    public double OverallScore { get; init; }
    public string Summary { get; init; } = "";
    public IList<ScoringRequirementResponse> Requirements { get; init; } = [];
}

/// <summary>A single requirement entry within an <see cref="AdScoringResponse"/>.</summary>
public sealed class ScoringRequirementResponse
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool IsOptional { get; init; }
    public double Score { get; init; }
    public string? Notes { get; init; }
}
