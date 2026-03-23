namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a registered job board.
/// </summary>
public record JobBoardResponse
{
    /// <summary>Gets the unique identifier of the job board.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the optional human-readable name of the board.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the seed URL of the job board.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the current status of the board (e.g. "pending", "active", "paused", "error").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the cron expression defining the scrape schedule.</summary>
    public required string ScheduleCron { get; init; }

    /// <summary>Gets the UTC timestamp of the most recent completed scrape run, or null if never scraped.</summary>
    public DateTimeOffset? LastScrapedAt { get; init; }

    /// <summary>Gets the UTC timestamp when the board was registered.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the UTC timestamp of the most recent update to this record.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the total number of job ads discovered for this board.</summary>
    public int AdCount { get; init; }

    /// <summary>Gets the AI-generated scraper configuration, or null if analysis has not yet completed.</summary>
    public ScraperConfigResponse? ScraperConfig { get; init; }

    /// <summary>Gets whether a scrape run is currently in progress for this board.</summary>
    public bool HasActiveRun { get; init; }
}
