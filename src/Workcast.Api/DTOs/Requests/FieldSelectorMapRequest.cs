namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Per-field CSS selectors evaluated relative to each job card element on a listing page.
/// Null means the field is not present or not identifiable on this board.
/// </summary>
public record FieldSelectorMapRequest
{
    /// <summary>Gets the CSS selector whose href attribute yields the job ad detail URL.</summary>
    public string? DetailUrl { get; init; }

    /// <summary>Gets the CSS selector for the job title text.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the CSS selector for the company name text.</summary>
    public string? Company { get; init; }

    /// <summary>Gets the CSS selector for the location text.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the CSS selector for the raw salary text.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>Gets the CSS selector for the posting date text.</summary>
    public string? PostedAt { get; init; }

    /// <summary>Gets the CSS selector for a short description snippet.</summary>
    public string? DescriptionSnippet { get; init; }

    /// <summary>Gets the CSS selector for a board-specific job identifier.</summary>
    public string? ExternalId { get; init; }
}
