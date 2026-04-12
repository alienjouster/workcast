namespace Workcast.Api.DTOs.Exchange;

/// <summary>
/// Portable representation of a scraper configuration used for import and export of job board configurations.
/// This DTO is the round-trip contract: it is both written on export and read on import.
/// </summary>
public record ScraperConfigExchangeDto
{
    // ── Pagination ──────────────────────────────────────────────────────────

    /// <summary>Pagination strategy used by this board's listing pages. One of: "url_param", "next_button", "infinite_scroll", "load_more_button", "none".</summary>
    public required string PaginationType { get; init; }

    /// <summary>CSS selector for the "next page" button, or null if not applicable.</summary>
    public string? NextPageSelector { get; init; }

    /// <summary>URL query parameter name used for pagination (e.g. "page"), or null.</summary>
    public string? UrlParamName { get; init; }

    /// <summary>True when the URL parameter represents an item offset rather than a page number.</summary>
    public bool UrlParamIsOffset { get; init; }

    /// <summary>Maximum number of pages to scrape as a safety cap. Null means unlimited.</summary>
    public int? MaxPages { get; init; }

    // ── Selectors ───────────────────────────────────────────────────────────

    /// <summary>CSS selector that identifies each job card element on a listing page.</summary>
    public required string JobCardSelector { get; init; }

    /// <summary>Per-field CSS selectors applied relative to each job card element.</summary>
    public required FieldSelectorMapExchangeDto FieldSelectors { get; init; }

    // ── Behavior ────────────────────────────────────────────────────────────

    /// <summary>True when the board requires JavaScript execution to render job listings.</summary>
    public required bool RequiresJs { get; init; }

    /// <summary>Recommended delay in milliseconds between requests (politeness delay).</summary>
    public required int SuggestedDelayMs { get; init; }

    // ── AI metadata (informational) ─────────────────────────────────────────

    /// <summary>AI confidence in the generated config, from 0.0 to 1.0.</summary>
    public float ConfidenceScore { get; init; }

    /// <summary>Free-text notes from the AI about unusual patterns on this board.</summary>
    public string? AnalyzerNotes { get; init; }

    /// <summary>UTC timestamp when this config was originally generated.</summary>
    public DateTimeOffset GeneratedAt { get; init; }
}
