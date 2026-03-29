using Workcast.Core.Enums;
using Workcast.Core.Models;

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
        var now = DateTimeOffset.UtcNow;
        return new Application
        {
            JobAdId = ad.Id,
            CreatedAt = now,
            Url = ad.Url,
            Title = ad.Title,
            Company = ad.Company,
            Location = ad.Location,
            SalaryRaw = ad.SalaryRaw,
            Description = ad.Description,
            PostedAt = ad.PostedAt,
            ScrapedAt = ad.ScrapedAt,
            ExternalId = ad.ExternalId,
            OverallScore = scoring?.OverallScore,
            ScoredAt = scoring?.ScoredAt,
            Summary = scoring?.Summary,
            Recommendation = scoring?.Recommendation,
            Requirements = scoring?.Requirements ?? [],
            Status = ApplicationStatus.ToApply,
            StatusHistory = [new StatusHistoryEntry { Status = ApplicationStatus.ToApply, AchievedAt = now }],
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

    /// <summary>UTC timestamp when the source job ad was scraped by Workcast.</summary>
    public DateTimeOffset ScrapedAt { get; private set; }

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

    /// <summary>True while an AI scoring job is in progress for this application.</summary>
    public bool IsScoringPending { get; private set; }

    /// <summary>Error message from the most recent failed scoring attempt, or null if scoring succeeded or was never run.</summary>
    public string? LastScoringError { get; private set; }

    /// <summary>True while a resume generation job is in progress for this application.</summary>
    public bool IsResumeGenerationPending { get; private set; }

    /// <summary>Error message from the most recent failed resume generation attempt, or null if it succeeded or was never run.</summary>
    public string? LastResumeGenerationError { get; private set; }

    // ── Status tracking ───────────────────────────────────────────────────────

    /// <summary>Current workflow stage of the application.</summary>
    public ApplicationStatus Status { get; private set; } = ApplicationStatus.ToApply;

    /// <summary>
    /// Ordered log of each status the application has reached and when.
    /// The current status is always present; moving backward does not remove prior entries.
    /// Stored as a JSONB array.
    /// </summary>
    public List<StatusHistoryEntry> StatusHistory { get; private set; } =
        [new StatusHistoryEntry { Status = ApplicationStatus.ToApply, AchievedAt = DateTimeOffset.UtcNow }];

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Sets or clears the date the job ad was originally posted.</summary>
    public void UpdatePostedAt(DateTimeOffset? postedAt) => PostedAt = postedAt;

    /// <summary>Updates the recorded scrape date for this application.</summary>
    public void UpdateScrapedAt(DateTimeOffset scrapedAt) => ScrapedAt = scrapedAt;

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

    /// <summary>Marks the application as having a scoring job in progress.</summary>
    public void SetScoringPending()
    {
        IsScoringPending = true;
        LastScoringError = null;
    }

    /// <summary>Clears the scoring-pending flag after a successful scoring run.</summary>
    public void ClearScoringPending() => IsScoringPending = false;

    /// <summary>Records a scoring failure and clears the pending flag.</summary>
    public void SetScoringFailed(string error)
    {
        IsScoringPending = false;
        LastScoringError = error;
    }

    /// <summary>Marks the application as having a resume generation job in progress.</summary>
    public void SetResumeGenerationPending()
    {
        IsResumeGenerationPending = true;
        LastResumeGenerationError = null;
    }

    /// <summary>Clears the resume-generation-pending flag after a successful run.</summary>
    public void ClearResumeGenerationPending() => IsResumeGenerationPending = false;

    /// <summary>Records a resume generation failure and clears the pending flag.</summary>
    public void SetResumeGenerationFailed(string error)
    {
        IsResumeGenerationPending = false;
        LastResumeGenerationError = error;
    }

    /// <summary>Applies a fresh scoring result to this application's snapshot fields.</summary>
    public void UpdateScoring(
        double overallScore,
        DateTimeOffset scoredAt,
        string? summary,
        string? recommendation,
        List<ScoringRequirement> requirements)
    {
        OverallScore  = overallScore;
        ScoredAt      = scoredAt;
        Summary       = summary;
        Recommendation = recommendation;
        Requirements  = requirements;
    }

    /// <summary>
    /// Transitions the application to <paramref name="newStatus"/>.
    /// <para>
    /// History entries for statuses that come <em>after</em> the new status are discarded —
    /// going backward clears future steps and their dates.
    /// The three Closed statuses are treated as mutually exclusive alternatives: switching
    /// between them replaces the previous Closed entry.
    /// </para>
    /// If <paramref name="achievedAt"/> is provided it is used as the history timestamp;
    /// otherwise the current UTC time is used for new entries, and existing entries keep
    /// their original date unless explicitly overridden.
    /// </summary>
    public void UpdateStatus(ApplicationStatus newStatus, DateTimeOffset? achievedAt = null)
    {
        // Build the trimmed history:
        // • Closed statuses are mutually exclusive — remove all other Closed entries.
        // • Non-Closed statuses use the enum integer order — remove everything after the new position.
        List<StatusHistoryEntry> trimmed = IsClosed(newStatus)
            ? StatusHistory.Where(e => !IsClosed(e.Status)).ToList()
            : StatusHistory.Where(e => (int)e.Status <= (int)newStatus).ToList();

        var existing = trimmed.FindIndex(e => e.Status == newStatus);
        if (existing >= 0)
        {
            // Only overwrite the date when the caller provides an explicit value.
            if (achievedAt.HasValue)
                trimmed = trimmed
                    .Select((e, i) => i == existing
                        ? new StatusHistoryEntry { Status = e.Status, AchievedAt = achievedAt.Value }
                        : e)
                    .ToList();
        }
        else
        {
            trimmed.Add(new StatusHistoryEntry
            {
                Status = newStatus,
                AchievedAt = achievedAt ?? DateTimeOffset.UtcNow,
            });
        }

        StatusHistory = trimmed;
        Status = newStatus;
    }

    private static bool IsClosed(ApplicationStatus status) =>
        status is ApplicationStatus.ClosedNoAnswer
               or ApplicationStatus.ClosedRejected
               or ApplicationStatus.ClosedHired;

    /// <summary>
    /// Updates only the recorded date for a status that is already in the history.
    /// Does nothing if the status has not been reached yet.
    /// </summary>
    public void UpdateStatusDate(ApplicationStatus status, DateTimeOffset achievedAt)
    {
        var existing = StatusHistory.FindIndex(e => e.Status == status);
        if (existing < 0) return;

        StatusHistory = StatusHistory
            .Select((e, i) => i == existing
                ? new StatusHistoryEntry { Status = e.Status, AchievedAt = achievedAt }
                : e)
            .ToList();
    }
}
