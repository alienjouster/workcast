using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Enums;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Recurring Hangfire job that marks orphaned scrape runs as failed.
/// A run becomes orphaned when the job worker is killed (process crash, forced Hangfire
/// cancellation, hung Playwright call) before the normal completion/failure path can execute.
/// Without this cleanup, such runs remain in the <see cref="RunStatus.Running"/> state
/// indefinitely and the UI shows them as still running.
/// Runs every 5 minutes. Any run still running after 30 minutes is considered stale.
/// </summary>
public sealed class StaleRunCleanupJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

    private readonly AppDbContext _dbContext;
    private readonly IEventBroadcaster _broadcaster;
    private readonly ILogger<StaleRunCleanupJob> _logger;

    public StaleRunCleanupJob(
        AppDbContext dbContext,
        IEventBroadcaster broadcaster,
        ILogger<StaleRunCleanupJob> logger)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - StaleThreshold;

        var staleRuns = await _dbContext.ScrapeRuns
            .Where(r => r.Status == RunStatus.Processing && r.StartedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (staleRuns.Count == 0) return;

        _logger.LogWarning(
            "Found {Count} stale scrape run(s) still marked Processing after {Minutes} min — marking as Failed",
            staleRuns.Count, (int)StaleThreshold.TotalMinutes);

        foreach (var run in staleRuns)
        {
            run.Fail(run.PagesScraped, run.AdsFound, run.AdsNew);

            _logger.LogWarning(
                "Stale run {RunId} for board {BoardId} (started {StartedAt}) marked Failed",
                run.Id, run.JobBoardId, run.StartedAt);
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var run in staleRuns)
        {
            await _broadcaster.PublishAsync(new WorkcastEvent
            {
                Type    = WorkcastEvent.RunCompleted,
                BoardId = run.JobBoardId,
                RunId   = run.Id,
                Status  = "failed",
            }).ConfigureAwait(false);
        }
    }
}
