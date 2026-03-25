namespace Workcast.Core.Entities;

/// <summary>
/// Represents a job application file created by the user from a job ad.
/// All job ad and scoring data is copied at creation time so the application
/// remains a complete historical record even if the source job ad is deleted.
/// </summary>
public sealed class Application
{
    private Application() { }

    /// <summary>
    /// Creates a new application by copying data from a job ad and its scoring result.
    /// </summary>
    public static Application CreateFromJobAd(JobAd ad, AdScoring? scoring)
    {
        return new Application
        {
            JobAdId = ad.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            Url = ad.Url,
            Title = ad.Title,
            Company = ad.Company,
            Location = ad.Location,
            SalaryRaw = ad.SalaryRaw,
            Description = ad.Description,
            PostedAt = ad.PostedAt,
            ExternalId = ad.ExternalId,
            OverallScore = scoring?.OverallScore,
            ScoredAt = scoring?.ScoredAt,
            Summary = scoring?.Summary,
            Recommendation = scoring?.Recommendation,
            Requirements = scoring?.Requirements ?? [],
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Reference to the source job ad. Nullable — the application survives
    /// independent of the job ad lifecycle. Set to null if the ad is deleted.
    /// </summary>
    public Guid? JobAdId { get; private set; }

    /// <summary>UTC timestamp when the application was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>True when the application has been moved to the trash bin.</summary>
    public bool IsTrashed { get; private set; }

    // ── Fields copied from JobAd ──────────────────────────────────────────────

    /// <summary>URL of the job ad detail page.</summary>
    public string Url { get; private set; } = "";

    /// <summary>Job title extracted from the listing page.</summary>
    public string? Title { get; private set; }

    /// <summary>Company name extracted from the listing page.</summary>
    public string? Company { get; private set; }

    /// <summary>Location extracted from the listing page.</summary>
    public string? Location { get; private set; }

    /// <summary>Raw salary string as it appeared on the listing page.</summary>
    public string? SalaryRaw { get; private set; }

    /// <summary>Description snippet extracted from the listing page.</summary>
    public string? Description { get; private set; }

    /// <summary>Date the ad was originally posted, if available.</summary>
    public DateTimeOffset? PostedAt { get; private set; }

    /// <summary>Board-specific external identifier for reference.</summary>
    public string? ExternalId { get; private set; }

    // ── Fields copied from AdScoring (nullable — scoring may not have existed) ─

    /// <summary>Overall resume-matching score (0–100) copied from scoring, or null if no scoring existed.</summary>
    public double? OverallScore { get; private set; }

    /// <summary>Timestamp when the scoring was performed, or null if no scoring existed.</summary>
    public DateTimeOffset? ScoredAt { get; private set; }

    /// <summary>AI-generated narrative summary, or null if no scoring existed.</summary>
    public string? Summary { get; private set; }

    /// <summary>AI-generated actionable recommendation, or null if no scoring existed.</summary>
    public string? Recommendation { get; private set; }

    /// <summary>Per-requirement scoring breakdown. Empty when no scoring existed at application time.</summary>
    public List<ScoringRequirement> Requirements { get; private set; } = [];

    /// <summary>
    /// Full text content of the job ad page, fetched at creation time by stripping HTML from the ad URL.
    /// Null when the fetch failed or returned fewer than 250 characters (page not accessible).
    /// </summary>
    public string? JobAdContent { get; private set; }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Moves this application to the trash bin.</summary>
    public void Trash() => IsTrashed = true;

    /// <summary>Restores this application from the trash bin.</summary>
    public void Restore() => IsTrashed = false;

    /// <summary>
    /// Clears the reference to the source job ad.
    /// Called when the source job ad is permanently deleted.
    /// </summary>
    public void ClearJobAdReference() => JobAdId = null;

    /// <summary>Updates the stored job ad page content. Pass null to clear it.</summary>
    public void UpdateJobAdContent(string? content) => JobAdContent = content;
}
