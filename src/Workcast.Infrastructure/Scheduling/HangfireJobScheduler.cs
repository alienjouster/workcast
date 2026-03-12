using System.Linq.Expressions;
using Hangfire;

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
}
