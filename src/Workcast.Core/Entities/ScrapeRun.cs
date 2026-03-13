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
    /// Creates a new <see cref="ScrapeRun"/> in the <see cref="RunStatus.Running"/> state.
    /// </summary>
    /// <param name="jobBoardId">The board being scraped.</param>
    /// <param name="triggeredBy">What initiated this run.</param>
    public static ScrapeRun Create(Guid jobBoardId, TriggerSource triggeredBy)
    {
        return new ScrapeRun
        {
            JobBoardId = jobBoardId,
            TriggeredBy = triggeredBy,
            StartedAt = DateTimeOffset.UtcNow,
            Status = RunStatus.Running,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

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
