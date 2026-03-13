using Workcast.Core.Enums;

namespace Workcast.Core.Models;

/// <summary>
/// Structured result returned by the AI provider after analyzing a job board page.
/// Maps directly to the <c>save_board_config</c> tool schema defined in TECHSPEC section 4.3.
/// The Infrastructure layer enriches this with <see cref="ScraperConfig.GeneratedAt"/> before persisting.
/// Job ad fields are extracted deterministically from the listing page using <see cref="FieldSelectors"/>
/// applied to each <see cref="JobCardSelector"/> element — no per-ad AI call is made.
/// </summary>
public record BoardAnalysisResult
{
    /// <summary>Pagination strategy detected on this board.</summary>
    public required PaginationType PaginationType { get; init; }

    /// <summary>CSS selector that identifies each job card element on a listing page.</summary>
    public required string JobCardSelector { get; init; }

    /// <summary>Per-field CSS selectors applied relative to each job card element.</summary>
    public required FieldSelectorMap FieldSelectors { get; init; }

    /// <summary>CSS selector for the "next page" button, or null if not applicable.</summary>
    public string? NextPageSelector { get; init; }

    /// <summary>URL query parameter name used for pagination, or null.</summary>
    public string? UrlParamName { get; init; }

    /// <summary>True when the URL parameter represents an item offset rather than a page number.</summary>
    public bool UrlParamIsOffset { get; init; }

    /// <summary>Maximum number of pages to scrape as a safety cap. Null means unlimited.</summary>
    public int? MaxPages { get; init; }

    /// <summary>True when the board requires JavaScript execution to render.</summary>
    public required bool RequiresJs { get; init; }

    /// <summary>Recommended delay in milliseconds between requests.</summary>
    public required int SuggestedDelayMs { get; init; }

    /// <summary>AI confidence in the generated config, from 0.0 to 1.0.</summary>
    public required float ConfidenceScore { get; init; }

    /// <summary>Free-text notes from the AI about unusual patterns on this board.</summary>
    public string? AnalyzerNotes { get; init; }

    /// <summary>Converts this result into a <see cref="ScraperConfig"/> ready for persistence.</summary>
    public ScraperConfig ToScraperConfig() => new()
    {
        PaginationType = PaginationType,
        JobCardSelector = JobCardSelector,
        FieldSelectors = FieldSelectors,
        NextPageSelector = NextPageSelector,
        UrlParamName = UrlParamName,
        UrlParamIsOffset = UrlParamIsOffset,
        MaxPages = MaxPages,
        RequiresJs = RequiresJs,
        SuggestedDelayMs = SuggestedDelayMs,
        ConfidenceScore = ConfidenceScore,
        AnalyzerNotes = AnalyzerNotes,
        GeneratedAt = DateTimeOffset.UtcNow,
    };
}
