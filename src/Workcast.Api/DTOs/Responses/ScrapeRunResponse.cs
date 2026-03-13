namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a scrape run execution record.
/// </summary>
public record ScrapeRunResponse
{
    /// <summary>Gets the unique identifier of the scrape run.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the identifier of the job board this run belongs to.</summary>
    public required Guid JobBoardId { get; init; }

    /// <summary>Gets the trigger source for this run (e.g. "scheduler", "manual").</summary>
    public required string TriggeredBy { get; init; }

    /// <summary>Gets the UTC timestamp when the run started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the UTC timestamp when the run finished, or null if still running.</summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Gets the current status of the run (e.g. "running", "completed", "failed", "partial").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the number of listing pages scraped during this run.</summary>
    public required int PagesScraped { get; init; }

    /// <summary>Gets the total number of job ads found during this run.</summary>
    public required int AdsFound { get; init; }

    /// <summary>Gets the number of new job ads persisted during this run (excluding duplicates).</summary>
    public required int AdsNew { get; init; }

    /// <summary>Gets the list of errors encountered during the run.</summary>
    public required IList<ScrapeRunErrorResponse> Errors { get; init; }
}
