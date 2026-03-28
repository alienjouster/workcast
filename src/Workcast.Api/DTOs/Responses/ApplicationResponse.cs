namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a user job application file.
/// All job ad and scoring fields are copied at creation time and live independently
/// of the source job ad.
/// </summary>
public record ApplicationResponse
{
    /// <summary>Gets the unique identifier of the application.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the identifier of the source job ad, or null if the ad has been deleted.</summary>
    public Guid? JobAdId { get; init; }

    /// <summary>Gets the UTC timestamp when the application was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets a value indicating whether this application is in the trash bin.</summary>
    public required bool IsTrashed { get; init; }

    // ── Copied from JobAd ────────────────────────────────────────────────────

    /// <summary>Gets the URL of the original job ad.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the job title.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the company name.</summary>
    public string? Company { get; init; }

    /// <summary>Gets the location.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the raw salary string.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>Gets the description snippet.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the date the ad was originally posted.</summary>
    public DateTimeOffset? PostedAt { get; init; }

    /// <summary>Gets the UTC timestamp when the source job ad was scraped by Workcast.</summary>
    public required DateTimeOffset ScrapedAt { get; init; }

    /// <summary>Gets the board-specific external identifier.</summary>
    public string? ExternalId { get; init; }

    // ── Copied from AdScoring ────────────────────────────────────────────────

    /// <summary>Gets the overall resume-matching score (0–100), or null if no scoring existed at application time.</summary>
    public double? OverallScore { get; init; }

    /// <summary>Gets the timestamp when scoring was performed, or null if no scoring existed.</summary>
    public DateTimeOffset? ScoredAt { get; init; }

    /// <summary>Gets the AI-generated narrative summary, or null if no scoring existed.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the AI-generated recommendation, or null if no scoring existed.</summary>
    public string? Recommendation { get; init; }

    /// <summary>Gets the per-requirement scoring breakdown. Empty when no scoring existed at application time.</summary>
    public required IList<ScoringRequirementResponse> Requirements { get; init; }

    /// <summary>
    /// Gets the full text content of the job ad page, fetched at creation time.
    /// Null when the fetch failed or returned fewer than 250 characters.
    /// </summary>
    public string? JobAdContent { get; init; }

    /// <summary>Gets a value indicating whether a scoring job is currently in progress.</summary>
    public required bool IsScoringPending { get; init; }

    /// <summary>Gets the error message from the most recent failed scoring attempt, or null.</summary>
    public string? LastScoringError { get; init; }

    // ── Status tracking ──────────────────────────────────────────────────────

    /// <summary>Gets the current workflow stage of the application as a string (e.g. "ToApply").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the ordered history of statuses the application has reached, with dates.</summary>
    public required IList<StatusHistoryEntryResponse> StatusHistory { get; init; }
}

/// <summary>A single entry in an application's status history.</summary>
public record StatusHistoryEntryResponse
{
    /// <summary>Gets the status as a string (e.g. "Applied").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the UTC timestamp when this status was reached.</summary>
    public required DateTimeOffset AchievedAt { get; init; }
}
