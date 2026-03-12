using Workcast.Core.Models;

namespace Workcast.Core.Entities;

/// <summary>
/// Represents a single job advertisement scraped from a registered job board.
/// </summary>
public class JobAd
{
    private JobAd() { }

    /// <summary>
    /// Creates a new <see cref="JobAd"/> with the raw HTML preserved for potential re-extraction.
    /// Extraction results are applied separately via <see cref="ApplyExtraction"/>.
    /// </summary>
    /// <param name="jobBoardId">The board this ad belongs to.</param>
    /// <param name="url">The canonical URL of this job ad's detail page.</param>
    /// <param name="rawHtml">The raw HTML of the detail page, stored for re-extraction.</param>
    /// <param name="scrapeRunId">The run that discovered this ad, or null.</param>
    public static JobAd Create(Guid jobBoardId, string url, string rawHtml, Guid? scrapeRunId = null)
    {
        return new JobAd
        {
            JobBoardId = jobBoardId,
            ScrapeRunId = scrapeRunId,
            Url = url,
            RawHtml = rawHtml,
            ScrapedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Foreign key to the owning <see cref="JobBoard"/>.</summary>
    public Guid JobBoardId { get; private set; }

    /// <summary>Foreign key to the <see cref="ScrapeRun"/> that discovered this ad. Nullable (SET NULL on run delete).</summary>
    public Guid? ScrapeRunId { get; private set; }

    /// <summary>Board-specific identifier extracted by the AI, used as a secondary deduplication key.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Canonical URL of this job ad's detail page.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>Job title extracted by the AI.</summary>
    public string? Title { get; private set; }

    /// <summary>Company name extracted by the AI.</summary>
    public string? Company { get; private set; }

    /// <summary>Location string extracted by the AI.</summary>
    public string? Location { get; private set; }

    /// <summary>Raw salary text as it appears on the page.</summary>
    public string? SalaryRaw { get; private set; }

    /// <summary>Full job description extracted by the AI.</summary>
    public string? Description { get; private set; }

    /// <summary>Date/time the ad was originally posted, as parsed from the AI extraction.</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    /// <summary>UTC timestamp when this ad was scraped.</summary>
    public DateTimeOffset ScrapedAt { get; private set; }

    /// <summary>Raw HTML of the detail page, stored to allow re-extraction without re-scraping.</summary>
    public string RawHtml { get; private set; } = string.Empty;

    /// <summary>AI confidence in the extraction quality, from 0.0 to 1.0.</summary>
    public float AiConfidenceScore { get; private set; }

    /// <summary>
    /// False when the ad is no longer visible on the board
    /// (not seen in the last 3 consecutive scrape runs).
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Navigation property — the owning job board.</summary>
    public JobBoard? JobBoard { get; private set; }

    /// <summary>Navigation property — the run that discovered this ad.</summary>
    public ScrapeRun? ScrapeRun { get; private set; }

    /// <summary>
    /// Applies the AI extraction result to this ad's fields.
    /// Called after the AI provider returns a <see cref="JobAdExtractionResult"/>.
    /// </summary>
    public void ApplyExtraction(JobAdExtractionResult result)
    {
        Title = result.Title;
        Company = result.Company;
        Location = result.Location;
        SalaryRaw = result.SalaryRaw;
        Description = result.Description;
        ExternalId = result.ExternalId;
        AiConfidenceScore = result.ConfidenceScore;

        if (result.PostedAt is not null &&
            DateTimeOffset.TryParse(result.PostedAt, out var parsedDate))
        {
            PostedAt = parsedDate;
        }
    }

    /// <summary>
    /// Marks this ad as inactive because it was not seen in recent scrape runs.
    /// See TECHSPEC section 5.3 for the stale detection policy.
    /// </summary>
    public void MarkInactive()
    {
        IsActive = false;
    }

    /// <summary>Marks this ad as active again if it reappears on the board.</summary>
    public void MarkActive()
    {
        IsActive = true;
    }
}
