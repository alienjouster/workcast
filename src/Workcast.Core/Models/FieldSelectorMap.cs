namespace Workcast.Core.Models;

/// <summary>
/// Maps each extractable job ad field to a CSS selector applied to a job card element.
/// All selectors are relative to the element matched by <see cref="ScraperConfig.JobCardSelector"/>.
/// A null selector means the field is not available on this board's listing page.
/// </summary>
public record FieldSelectorMap
{
    /// <summary>CSS selector whose <c>href</c> attribute yields the job ad detail URL.</summary>
    public string? DetailUrl { get; init; }

    /// <summary>CSS selector for the job title text.</summary>
    public string? Title { get; init; }

    /// <summary>CSS selector for the company name text.</summary>
    public string? Company { get; init; }

    /// <summary>CSS selector for the location text.</summary>
    public string? Location { get; init; }

    /// <summary>CSS selector for the raw salary text as it appears on the page.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>CSS selector for the date/time the ad was posted.</summary>
    public string? PostedAt { get; init; }

    /// <summary>CSS selector for a short description snippet visible on the listing page.</summary>
    public string? DescriptionSnippet { get; init; }

    /// <summary>CSS selector or attribute expression for a board-specific job identifier used for deduplication.</summary>
    public string? ExternalId { get; init; }
}
