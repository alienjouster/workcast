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

    /// <summary>
    /// Creates a new manually-entered <see cref="JobAd"/> that is not tied to any job board or scrape run.
    /// </summary>
    /// <param name="url">The URL of the job ad.</param>
    /// <param name="title">The job title.</param>
    /// <param name="company">Optional company name.</param>
    /// <param name="location">Optional location.</param>
    public static JobAd CreateManual(string url, string title, string? company, string? location)
    {
        return new JobAd
        {
            Url = url,
            Title = title,
            Company = company,
            Location = location,
            ScrapedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            IsManual = true,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Foreign key to the owning <see cref="JobBoard"/>. Null for manually-entered ads.</summary>
    public Guid? JobBoardId { get; private set; }

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

    /// <summary>
    /// True when the user has pinned this ad so it always appears at the top of the list.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>
    /// True when the user has opened or explicitly marked this ad as read.
    /// Defaults to false so freshly scraped ads appear as unread.
    /// </summary>
    public bool IsRead { get; private set; }

    /// <summary>
    /// True when the user has moved this ad to the trash bin.
    /// Trashed ads are hidden from the main list but not hard-deleted until the nightly
    /// cleanup job removes them (along with inactive ads) after 30 days.
    /// </summary>
    public bool IsTrashed { get; private set; }

    /// <summary>
    /// True while a scoring Hangfire job for this ad is queued or executing.
    /// Set by the controller on enqueue, cleared by <see cref="Workcast.Jobs.AdScoringJob"/>
    /// on completion (success or failure) so the UI can track in-progress state.
    /// </summary>
    public bool IsScoringPending { get; private set; }

    /// <summary>
    /// True when this ad was manually entered by the user rather than scraped automatically.
    /// Manual ads are not tied to any job board or scrape run.
    /// </summary>
    public bool IsManual { get; private set; }

    /// <summary>
    /// Human-readable error message from the last failed scoring attempt, or null if the
    /// last scoring succeeded or has never been run.
    /// </summary>
    public string? LastScoringError { get; private set; }

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

    /// <summary>Pins this ad so it appears at the top of all job ad lists.</summary>
    public void Pin()
    {
        IsPinned = true;
    }

    /// <summary>Unpins this ad, returning it to its natural sort position.</summary>
    public void Unpin()
    {
        IsPinned = false;
    }

    /// <summary>Marks this ad as read.</summary>
    public void MarkRead()
    {
        IsRead = true;
    }

    /// <summary>Marks this ad as unread.</summary>
    public void MarkUnread()
    {
        IsRead = false;
    }

    /// <summary>Moves this ad to the trash bin.</summary>
    public void Trash()
    {
        IsTrashed = true;
    }

    /// <summary>Restores this ad from the trash bin back to the main list.</summary>
    public void Restore()
    {
        IsTrashed = false;
    }

    /// <summary>Marks scoring as in-progress for this ad and clears any previous error.</summary>
    public void SetScoringPending()
    {
        IsScoringPending = true;
        LastScoringError = null;
    }

    /// <summary>Clears the scoring-in-progress flag once the job completes successfully.</summary>
    public void ClearScoringPending()
    {
        IsScoringPending = false;
        LastScoringError = null;
    }

    /// <summary>Clears the scoring-in-progress flag and records the failure reason.</summary>
    public void SetScoringFailed(string error)
    {
        IsScoringPending = false;
        LastScoringError = error;
    }

    /// <summary>
    /// Free-text personal note written by the user about this ad
    /// (e.g. "Applied on 2026-03-22", "I know the hiring manager").
    /// Null when no note has been written.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>Sets or clears the personal note for this ad. Pass null or empty to delete.</summary>
    public void SetNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// Updates the user-editable details of a job ad (URL, title, company, location).
    /// Intended for use with manually-entered ads but not restricted to them.
    /// </summary>
    public void UpdateDetails(string url, string title, string? company, string? location)
    {
        Url = url;
        Title = title;
        Company = company;
        Location = location;
    }
}
