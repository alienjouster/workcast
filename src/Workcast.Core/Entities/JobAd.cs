namespace Workcast.Core.Entities;

/// <summary>
/// Represents a single job advertisement scraped from a registered job board.
/// Fields are populated deterministically via CSS selectors defined in the board's
/// <see cref="Workcast.Core.Models.ScraperConfig.FieldSelectors"/> — no per-ad AI call is made.
/// </summary>
public class JobAd
{
    private JobAd() { }

    /// <summary>
    /// Creates a new <see cref="JobAd"/> shell for a discovered job card.
    /// Field values are applied separately via <see cref="ApplyExtraction"/>.
    /// </summary>
    /// <param name="jobBoardId">The board this ad belongs to.</param>
    /// <param name="url">The canonical URL of this job ad's detail page.</param>
    /// <param name="scrapeRunId">The run that discovered this ad, or null.</param>
    public static JobAd Create(Guid jobBoardId, string url, Guid? scrapeRunId = null)
    {
        return new JobAd
        {
            JobBoardId = jobBoardId,
            ScrapeRunId = scrapeRunId,
            Url = url,
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

    /// <summary>Board-specific identifier extracted from the job card, used as a secondary deduplication key.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Canonical URL of this job ad's detail page.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>Job title extracted from the job card via CSS selector.</summary>
    public string? Title { get; private set; }

    /// <summary>Company name extracted from the job card via CSS selector.</summary>
    public string? Company { get; private set; }

    /// <summary>Location string extracted from the job card via CSS selector.</summary>
    public string? Location { get; private set; }

    /// <summary>Raw salary text as it appears on the listing page.</summary>
    public string? SalaryRaw { get; private set; }

    /// <summary>Short description snippet extracted from the job card via CSS selector.</summary>
    public string? Description { get; private set; }

    /// <summary>Date/time the ad was originally posted, parsed from the raw value extracted via CSS selector.</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    /// <summary>UTC timestamp when this ad was scraped.</summary>
    public DateTimeOffset ScrapedAt { get; private set; }

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
    /// Applies deterministically-extracted field values from the job card to this ad.
    /// All values are obtained via CSS selectors — no AI call is involved.
    /// <paramref name="postedAt"/> is parsed leniently; if parsing fails the field is left null.
    /// </summary>
    /// <param name="title">Job title text.</param>
    /// <param name="company">Company name text.</param>
    /// <param name="location">Location text.</param>
    /// <param name="salaryRaw">Raw salary text as it appears on the page.</param>
    /// <param name="postedAt">Date/time string for when the ad was posted.</param>
    /// <param name="externalId">Board-specific job identifier for deduplication.</param>
    /// <param name="descriptionSnippet">Short description snippet from the listing page.</param>
    public void ApplyExtraction(
        string? title,
        string? company,
        string? location,
        string? salaryRaw,
        string? postedAt,
        string? externalId,
        string? descriptionSnippet)
    {
        Title = title;
        Company = company;
        Location = location;
        SalaryRaw = salaryRaw;
        ExternalId = externalId;
        Description = descriptionSnippet;

        if (postedAt is not null &&
            DateTimeOffset.TryParse(postedAt, out var parsedDate))
        {
            PostedAt = parsedDate.ToUniversalTime();
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
