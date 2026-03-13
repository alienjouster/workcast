using System.Linq.Expressions;
using Hangfire;
using Hangfire.Storage.Monitoring;

namespace Workcast.Infrastructure.Scheduling;

/// <summary>
/// DI-injectable wrapper around Hangfire's static scheduling API.
/// Provides a testable abstraction for recurring job registration, removal, and fire-and-forget
/// enqueueing. Callers in Workcast.Jobs and Workcast.Api inject this service instead of
/// calling Hangfire's static methods directly.
/// See TECHSPEC section 7.3 for the scheduling patterns implemented here.
/// </summary>
public sealed class HangfireJobScheduler
{
    /// <summary>
    /// Registers or updates a Hangfire recurring job.
    /// If a recurring job with <paramref name="jobId"/> already exists, its schedule is updated.
    /// </summary>
    /// <typeparam name="T">The Hangfire job class to invoke.</typeparam>
    /// <param name="jobId">Stable unique identifier for the recurring job (e.g. "scrape-{boardId}").</param>
    /// <param name="methodCall">Expression identifying the method to invoke on <typeparamref name="T"/>.</param>
    /// <param name="cronExpression">Cron expression defining the schedule.</param>
    public void AddOrUpdateRecurring<T>(
        string jobId,
        Expression<Func<T, Task>> methodCall,
        string cronExpression)
    {
        RecurringJob.AddOrUpdate<T>(
            jobId,
            methodCall,
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    /// <summary>
    /// Removes a Hangfire recurring job if it exists. No-op if the job does not exist.
    /// </summary>
    /// <param name="jobId">The recurring job identifier to remove.</param>
    public void RemoveIfExists(string jobId)
    {
        RecurringJob.RemoveIfExists(jobId);
    }

    /// <summary>
    /// Enqueues a fire-and-forget Hangfire job for immediate execution.
    /// </summary>
    /// <typeparam name="T">The Hangfire job class to invoke.</typeparam>
    /// <param name="methodCall">Expression identifying the method to invoke on <typeparamref name="T"/>.</param>
    public void Enqueue<T>(Expression<Func<T, Task>> methodCall)
    {
        BackgroundJob.Enqueue<T>(methodCall);
    }

    /// <summary>
    /// Cancels all enqueued, scheduled, and processing Hangfire jobs whose first argument
    /// matches <paramref name="boardId"/>. Covers both <c>BoardAnalysisJob</c> and
    /// <c>ScrapeJobRunner</c> fire-and-forget jobs enqueued for this board.
    /// Note: a job already being executed by a worker is marked Deleted but cannot be
    /// interrupted mid-execution — that is a Hangfire platform limitation.
    /// </summary>
    /// <param name="boardId">The board ID to match against job arguments.</param>
    public void DeleteBoardJobs(Guid boardId)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var toDelete = new List<string>();

        foreach (var queue in monitoring.Queues())
        {
            var enqueued = monitoring.EnqueuedJobs(queue.Name, 0, 1000);
            toDelete.AddRange(enqueued
                .Where(kvp => IsBoardJob(kvp.Value.Job, boardId))
                .Select(kvp => kvp.Key));
        }

        toDelete.AddRange(monitoring.ScheduledJobs(0, 1000)
            .Where(kvp => IsBoardJob(kvp.Value.Job, boardId))
            .Select(kvp => kvp.Key));

        toDelete.AddRange(monitoring.ProcessingJobs(0, 1000)
            .Where(kvp => IsBoardJob(kvp.Value.Job, boardId))
            .Select(kvp => kvp.Key));

        foreach (var jobId in toDelete)
            BackgroundJob.Delete(jobId);
    }

    private static bool IsBoardJob(Hangfire.Common.Job? job, Guid boardId) =>
        job?.Args?.Count > 0 && job.Args[0] is Guid id && id == boardId;
}
