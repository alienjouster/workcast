namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a scraped job advertisement.
/// Field values are extracted deterministically from the listing page via CSS selectors —
/// no per-ad AI call is made.
/// </summary>
public record JobAdResponse
{
    /// <summary>Gets the unique identifier of the job ad.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the identifier of the job board this ad belongs to.</summary>
    public required Guid JobBoardId { get; init; }

    /// <summary>Gets the identifier of the scrape run that discovered this ad, if available.</summary>
    public Guid? ScrapeRunId { get; init; }

    /// <summary>Gets the board-specific external identifier for deduplication, if extracted.</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets the URL of the job ad detail page.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the job title extracted via CSS selector.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the company name extracted via CSS selector.</summary>
    public string? Company { get; init; }

    /// <summary>Gets the location extracted via CSS selector.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the raw salary string as it appeared on the listing page.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>Gets the description snippet extracted via CSS selector from the listing page.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the date the ad was originally posted, if extracted and parseable.</summary>
    public DateTimeOffset? PostedAt { get; init; }

    /// <summary>Gets the UTC timestamp when this ad was scraped.</summary>
    public required DateTimeOffset ScrapedAt { get; init; }

    /// <summary>Gets a value indicating whether this ad is still active on the board.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets a value indicating whether this ad is pinned to the top of the list.</summary>
    public required bool IsPinned { get; init; }

    /// <summary>Gets a value indicating whether the user has read this ad.</summary>
    public required bool IsRead { get; init; }

    /// <summary>Gets the overall resume-matching score (0–100), or null if no scoring has been run.</summary>
    public double? OverallScore { get; init; }

    /// <summary>Gets a value indicating whether a scoring job is currently queued or running for this ad.</summary>
    public required bool IsScoringPending { get; init; }

    /// <summary>Gets a value indicating whether this ad has been moved to the trash bin.</summary>
    public required bool IsTrashed { get; init; }

    /// <summary>Gets the personal note written by the user, or null if none.</summary>
    public string? Note { get; init; }

    /// <summary>Gets the error message from the last failed scoring attempt, or null if scoring succeeded or has never been run.</summary>
    public string? LastScoringError { get; init; }
}
