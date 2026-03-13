namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of the AI-generated scraper configuration for a job board.
/// </summary>
public record ScraperConfigResponse
{
    /// <summary>Gets the pagination strategy used by this board (e.g. "url_param", "next_button", "infinite_scroll", "none").</summary>
    public required string PaginationType { get; init; }

    /// <summary>Gets the CSS selector that identifies each job card element on a listing page.</summary>
    public required string JobCardSelector { get; init; }

    /// <summary>Gets the per-field CSS selectors applied relative to each job card element.</summary>
    public required FieldSelectorMapResponse FieldSelectors { get; init; }

    /// <summary>Gets the CSS selector for the "next page" button, if applicable.</summary>
    public string? NextPageSelector { get; init; }

    /// <summary>Gets the URL query parameter name used for pagination, if applicable.</summary>
    public string? UrlParamName { get; init; }

    /// <summary>Gets a value indicating whether the URL pagination parameter is an item offset rather than a page number.</summary>
    public bool UrlParamIsOffset { get; init; }

    /// <summary>Gets the maximum number of pages to scrape per run. Null means unlimited.</summary>
    public int? MaxPages { get; init; }

    /// <summary>Gets a value indicating whether the board requires JavaScript rendering.</summary>
    public required bool RequiresJs { get; init; }

    /// <summary>Gets the suggested delay in milliseconds between page requests for politeness.</summary>
    public required int SuggestedDelayMs { get; init; }

    /// <summary>Gets the AI confidence score for this configuration, from 0.0 to 1.0.</summary>
    public required float ConfidenceScore { get; init; }

    /// <summary>Gets optional free-text notes from the AI analyzer about unusual board patterns.</summary>
    public string? AnalyzerNotes { get; init; }

    /// <summary>Gets the UTC timestamp when this configuration was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
