namespace Workcast.Core.Models;

/// <summary>
/// Structured result returned by the AI provider after extracting a job ad from a detail page.
/// Maps directly to the <c>save_job_ad</c> tool schema defined in TECHSPEC section 4.4.
/// </summary>
public record JobAdExtractionResult
{
    /// <summary>Job title extracted from the page. Always present per tool schema.</summary>
    public required string Title { get; init; }

    /// <summary>Company name, or null if not found.</summary>
    public string? Company { get; init; }

    /// <summary>Location string, or null if not found.</summary>
    public string? Location { get; init; }

    /// <summary>Raw salary text as it appears on the page, or null if not found.</summary>
    public string? SalaryRaw { get; init; }

    /// <summary>Job description text, or null if not found.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Date/time the ad was posted, as a raw string from the AI.
    /// Stored as a string to preserve the original value; callers should parse if needed.
    /// </summary>
    public string? PostedAt { get; init; }

    /// <summary>Board-specific job identifier useful for deduplication, or null.</summary>
    public string? ExternalId { get; init; }

    /// <summary>AI confidence in the extraction quality, from 0.0 to 1.0.</summary>
    public required float ConfidenceScore { get; init; }
}
