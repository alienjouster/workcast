namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a scraped job advertisement.
/// </summary>
public record JobAdResponse
{
    /// <summary>Gets the unique identifier of the job ad.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the identifier of the job board this ad belongs to.</summary>
    public required Guid JobBoardId { get; init; }

    /// <summary>Gets the identifier of the scrape run that discovered this ad, if available.</summary>
    public Guid? ScrapeRunId { get; init; }

    /// <summary>Gets the board-specific external identifier for deduplication, if the AI extracted one.</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets the URL of the job ad detail page.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the job title extracted by AI.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the company name extracted by AI.</summary>
    public string? Company { get; init; }

    /// <summary>Gets the location extracted by AI.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the raw salary string as it appeared on the page.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>Gets the job description extracted by AI.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the date the ad was originally posted, if the AI could determine it.</summary>
    public DateTimeOffset? PostedAt { get; init; }

    /// <summary>Gets the UTC timestamp when this ad was scraped.</summary>
    public required DateTimeOffset ScrapedAt { get; init; }

    /// <summary>Gets the AI confidence score for the extraction, from 0.0 to 1.0.</summary>
    public required float AiConfidenceScore { get; init; }

    /// <summary>Gets a value indicating whether this ad is still active on the board.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets the raw HTML of the ad detail page. Only included in single-resource responses.</summary>
    public string? RawHtml { get; init; }
}
