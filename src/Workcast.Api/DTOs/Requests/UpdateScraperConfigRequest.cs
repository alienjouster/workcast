namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for manually replacing a job board's scraper configuration.
/// All selector fields are optional — null means "not used / not present on this board".
/// </summary>
public record UpdateScraperConfigRequest
{
    /// <summary>Gets the pagination strategy (url_param | next_button | infinite_scroll | load_more_button | none).</summary>
    public required string PaginationType { get; init; }

    /// <summary>Gets the CSS selector that matches each job card container on the listing page.</summary>
    public required string JobCardSelector { get; init; }

    /// <summary>Gets the per-field CSS selectors applied relative to each job card.</summary>
    public required FieldSelectorMapRequest FieldSelectors { get; init; }

    /// <summary>Gets the CSS selector for the next-page button, or null.</summary>
    public string? NextPageSelector { get; init; }

    /// <summary>Gets the URL query parameter name used for pagination, or null.</summary>
    public string? UrlParamName { get; init; }

    /// <summary>Gets a value indicating whether the URL pagination parameter is an item offset.</summary>
    public bool UrlParamIsOffset { get; init; }

    /// <summary>Gets the maximum number of pages to scrape per run. Null means unlimited.</summary>
    public int? MaxPages { get; init; }

    /// <summary>Gets a value indicating whether the board requires JavaScript rendering.</summary>
    public required bool RequiresJs { get; init; }

    /// <summary>Gets the suggested delay in milliseconds between page requests.</summary>
    public required int SuggestedDelayMs { get; init; }

    /// <summary>Gets optional free-text notes about unusual patterns on this board.</summary>
    public string? AnalyzerNotes { get; init; }
}
