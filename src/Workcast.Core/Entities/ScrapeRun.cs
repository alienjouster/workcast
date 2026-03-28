using Workcast.Core.Enums;
using Workcast.Core.Models;

namespace Workcast.Core.Entities;

/// <summary>
/// Records the execution of a single scraping pipeline run for a specific job board.
/// </summary>
public class ScrapeRun
{
    private ScrapeRun() { }

    /// <summary>
    /// Creates a new <see cref="ScrapeRun"/> in the <see cref="RunStatus.Enqueued"/> state.
    /// The run is persisted immediately at enqueue time so it appears in the UI before
    /// a Hangfire worker picks it up. Status is advanced to <see cref="RunStatus.Processing"/>
    /// by <see cref="ScrapeRunStateFilter"/> when Hangfire transitions to ProcessingState.
    /// </summary>
    /// <param name="jobBoardId">The board being scraped.</param>
    /// <param name="triggeredBy">What initiated this run.</param>
    /// <param name="hangfireJobId">The Hangfire job ID used to correlate state-filter events with this run.</param>
    public static ScrapeRun Create(Guid jobBoardId, TriggerSource triggeredBy, string hangfireJobId)
    {
        return new ScrapeRun
        {
            JobBoardId = jobBoardId,
            TriggeredBy = triggeredBy,
            StartedAt = DateTimeOffset.UtcNow,
            Status = RunStatus.Enqueued,
            HangfireJobId = hangfireJobId,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Hangfire job ID (string) used to correlate <see cref="ScrapeRunStateFilter"/> state
    /// transitions with this run. Set once at enqueue time and never changes, even across retries.
    /// </summary>
    public string HangfireJobId { get; private set; } = string.Empty;

    /// <summary>Foreign key to the owning <see cref="JobBoard"/>.</summary>
    public Guid JobBoardId { get; private set; }

    /// <summary>What triggered this run.</summary>
    public TriggerSource TriggeredBy { get; private set; }

    /// <summary>UTC timestamp when the run started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>UTC timestamp when the run finished. Null while still running.</summary>
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Current execution status of this run.</summary>
    public RunStatus Status { get; private set; }

    /// <summary>Number of listing pages visited during this run.</summary>
    public int PagesScraped { get; private set; }

    /// <summary>Total number of job ad links encountered (including duplicates).</summary>
    public int AdsFound { get; private set; }

    /// <summary>Number of job ads that were new (not already in the database).</summary>
    public int AdsNew { get; private set; }

    /// <summary>
    /// Non-fatal errors encountered during this run.
    /// Stored as JSONB. A run with errors may still complete as <see cref="RunStatus.Partial"/>.
    /// </summary>
    public IList<ScrapeRunError> Errors { get; private set; } = new List<ScrapeRunError>();

    /// <summary>Navigation property — the owning job board.</summary>
    public JobBoard? JobBoard { get; private set; }

    /// <summary>Navigation property — all job ads discovered during this run.</summary>
    public ICollection<JobAd> JobAds { get; private set; } = new List<JobAd>();

    /// <summary>
    /// Marks the run as <see cref="RunStatus.Completed"/> and records the final counts.
    /// </summary>
    public void Complete(int pagesScraped, int adsFound, int adsNew)
    {
        Status = RunStatus.Completed;
        FinishedAt = DateTimeOffset.UtcNow;
        PagesScraped = pagesScraped;
        AdsFound = adsFound;
        AdsNew = adsNew;
    }

    /// <summary>
    /// Marks the run as <see cref="RunStatus.Partial"/> — completed but with non-fatal errors.
    /// </summary>
    public void CompletePartial(int pagesScraped, int adsFound, int adsNew)
    {
        Status = RunStatus.Partial;
        FinishedAt = DateTimeOffset.UtcNow;
        PagesScraped = pagesScraped;
        AdsFound = adsFound;
        AdsNew = adsNew;
    }

    /// <summary>
    /// Marks the run as <see cref="RunStatus.Failed"/> due to a fatal error.
    /// Records the final counts at the point of failure.
    /// </summary>
    public void Fail(int pagesScraped, int adsFound, int adsNew)
    {
        Status = RunStatus.Failed;
        FinishedAt = DateTimeOffset.UtcNow;
        PagesScraped = pagesScraped;
        AdsFound = adsFound;
        AdsNew = adsNew;
    }

    /// <summary>
    /// Transitions the run to <see cref="RunStatus.Processing"/> when a Hangfire worker picks it up.
    /// Called by <see cref="ScrapeRunStateFilter"/> on Hangfire's ProcessingState.
    /// </summary>
    public void Start()
    {
        Status = RunStatus.Processing;
    }

    /// <summary>
    /// Sets the run status to an intermediate Hangfire-driven state without changing timestamps.
    /// Used by <see cref="ScrapeRunStateFilter"/> for <see cref="RunStatus.Scheduled"/>,
    /// <see cref="RunStatus.Awaiting"/>, and <see cref="RunStatus.Enqueued"/> (retry cycles).
    /// </summary>
    public void SetStatus(RunStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// Marks the run as <see cref="RunStatus.Deleted"/> when the job is removed from
    /// the Hangfire dashboard. Sets <see cref="FinishedAt"/> so the run is no longer
    /// considered active.
    /// </summary>
    public void Delete()
    {
        Status = RunStatus.Deleted;
        FinishedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Appends a non-fatal error encountered while processing a specific page.
    /// Errors accumulate without stopping the run.
    /// </summary>
    public void AddError(string page, string message)
    {
        Errors.Add(new ScrapeRunError
        {
            Page = page,
            Message = message,
            Timestamp = DateTimeOffset.UtcNow,
        });
    }
}
