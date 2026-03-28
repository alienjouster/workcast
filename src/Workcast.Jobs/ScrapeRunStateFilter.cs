using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Workcast.Core.Enums;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Global Hangfire state filter that keeps <see cref="Core.Entities.ScrapeRun"/> status
/// in sync with the Hangfire job state machine for all <see cref="ScrapeJobRunner"/> jobs.
/// <para>
/// Lifecycle:
/// <list type="bullet">
///   <item><see cref="EnqueuedState"/> (first time) — creates the <c>ScrapeRun</c> record in
///     <see cref="RunStatus.Enqueued"/> and stores the run ID as a Hangfire job parameter.</item>
///   <item><see cref="EnqueuedState"/> (retry cycle) — resets an existing run back to
///     <see cref="RunStatus.Enqueued"/>.</item>
///   <item><see cref="ScheduledState"/> — sets run to <see cref="RunStatus.Scheduled"/>
///     (retry back-off delay).</item>
///   <item><see cref="AwaitingState"/> — sets run to <see cref="RunStatus.Awaiting"/>.</item>
///   <item><see cref="ProcessingState"/> — calls <c>run.Start()</c> →
///     <see cref="RunStatus.Processing"/> and broadcasts <c>RunStarted</c>.</item>
///   <item><see cref="SucceededState"/> — skipped; the job already set
///     <see cref="RunStatus.Completed"/> or <see cref="RunStatus.Partial"/>.</item>
///   <item><see cref="FailedState"/> — sets run to <see cref="RunStatus.Failed"/> only when
///     the job did not already record a terminal status (edge case: crash before try/catch).</item>
///   <item><see cref="DeletedState"/> — calls <c>run.Delete()</c> →
///     <see cref="RunStatus.Deleted"/>.</item>
/// </list>
/// </para>
/// Registered as a singleton in <c>Workcast.Jobs.DependencyInjection</c> and added to
/// <c>Hangfire.GlobalJobFilters.Filters</c> in <c>Program.cs</c>.
/// </summary>
public sealed class ScrapeRunStateFilter : IApplyStateFilter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBroadcaster _broadcaster;
    private readonly ILogger<ScrapeRunStateFilter> _logger;

    /// <summary>
    /// Initializes the filter with DI dependencies. Registered as a singleton; the
    /// <see cref="IServiceScopeFactory"/> is used to create a short-lived scope per event
    /// so that the scoped <see cref="AppDbContext"/> is disposed after each state transition.
    /// </summary>
    public ScrapeRunStateFilter(
        IServiceScopeFactory scopeFactory,
        IEventBroadcaster broadcaster,
        ILogger<ScrapeRunStateFilter> logger)
    {
        _scopeFactory = scopeFactory;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.BackgroundJob.Job.Type != typeof(ScrapeJobRunner)) return;

        var args = context.BackgroundJob.Job.Args;
        if (args.Count < 1 || args[0] is not Guid boardId) return;

        var triggerSource = args.Count >= 2
            ? (TriggerSource)Convert.ToInt32(args[1])
            : TriggerSource.Scheduler;

        try
        {
            HandleStateTransitionAsync(context, transaction, boardId, triggerSource)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Filter failures must not disrupt the Hangfire state machine.
            _logger.LogError(ex,
                "ScrapeRunStateFilter failed on {State} transition for Hangfire job {JobId} (board {BoardId})",
                context.NewState.Name, context.BackgroundJob.Id, boardId);
        }
    }

    /// <inheritdoc />
    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }

    // -------------------------------------------------------------------------

    private async Task HandleStateTransitionAsync(
        ApplyStateContext context,
        IWriteOnlyTransaction transaction,
        Guid boardId,
        TriggerSource triggerSource)
    {
        var hangfireJobId = context.BackgroundJob.Id;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (context.NewState)
        {
            // -----------------------------------------------------------------
            // EnqueuedState — create run on first enqueue; reset status on retry
            // -----------------------------------------------------------------
            case EnqueuedState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null)
                {
                    run = Core.Entities.ScrapeRun.Create(boardId, triggerSource, hangfireJobId);
                    db.ScrapeRuns.Add(run);
                    await db.SaveChangesAsync().ConfigureAwait(false);

                    await _broadcaster.PublishAsync(new WorkcastEvent
                    {
                        Type    = WorkcastEvent.RunEnqueued,
                        BoardId = boardId,
                        RunId   = run.Id,
                        Status  = "enqueued",
                    }).ConfigureAwait(false);
                }
                else
                {
                    // Retry cycle: restore from Failed/Scheduled back to Enqueued
                    run.SetStatus(RunStatus.Enqueued);
                    await db.SaveChangesAsync().ConfigureAwait(false);

                    await _broadcaster.PublishAsync(new WorkcastEvent
                    {
                        Type    = WorkcastEvent.RunStatusChanged,
                        BoardId = boardId,
                        RunId   = run.Id,
                        Status  = "enqueued",
                    }).ConfigureAwait(false);
                }
                break;
            }

            // -----------------------------------------------------------------
            // ScheduledState — retry back-off delay or delayed job
            // -----------------------------------------------------------------
            case ScheduledState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null) break;

                run.SetStatus(RunStatus.Scheduled);
                await db.SaveChangesAsync().ConfigureAwait(false);

                await _broadcaster.PublishAsync(new WorkcastEvent
                {
                    Type    = WorkcastEvent.RunStatusChanged,
                    BoardId = boardId,
                    RunId   = run.Id,
                    Status  = "scheduled",
                }).ConfigureAwait(false);
                break;
            }

            // -----------------------------------------------------------------
            // AwaitingState — continuation waiting for parent job
            // -----------------------------------------------------------------
            case AwaitingState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null) break;

                run.SetStatus(RunStatus.Awaiting);
                await db.SaveChangesAsync().ConfigureAwait(false);

                await _broadcaster.PublishAsync(new WorkcastEvent
                {
                    Type    = WorkcastEvent.RunStatusChanged,
                    BoardId = boardId,
                    RunId   = run.Id,
                    Status  = "awaiting",
                }).ConfigureAwait(false);
                break;
            }

            // -----------------------------------------------------------------
            // ProcessingState — worker picked up the job
            // -----------------------------------------------------------------
            case ProcessingState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null) break;

                run.Start();
                await db.SaveChangesAsync().ConfigureAwait(false);

                await _broadcaster.PublishAsync(new WorkcastEvent
                {
                    Type    = WorkcastEvent.RunStarted,
                    BoardId = boardId,
                    RunId   = run.Id,
                }).ConfigureAwait(false);
                break;
            }

            // -----------------------------------------------------------------
            // SucceededState — job returned normally; the job already set
            // Completed or Partial with correct counters — nothing to do here.
            // -----------------------------------------------------------------

            // -----------------------------------------------------------------
            // FailedState — job threw an unhandled exception. The job's own
            // catch blocks set Failed with counters before rethrowing, so we
            // only act when the run has not yet reached a terminal state
            // (edge case: crash before the try block).
            // -----------------------------------------------------------------
            case FailedState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null) break;
                if (run.Status is RunStatus.Completed or RunStatus.Partial or RunStatus.Failed) break;

                run.Fail(run.PagesScraped, run.AdsFound, run.AdsNew);
                await db.SaveChangesAsync().ConfigureAwait(false);

                await _broadcaster.PublishAsync(new WorkcastEvent
                {
                    Type    = WorkcastEvent.RunCompleted,
                    BoardId = boardId,
                    RunId   = run.Id,
                    Status  = "failed",
                }).ConfigureAwait(false);
                break;
            }

            // -----------------------------------------------------------------
            // DeletedState — job was manually removed from the Hangfire dashboard
            // -----------------------------------------------------------------
            case DeletedState:
            {
                var run = await db.ScrapeRuns
                    .FirstOrDefaultAsync(r => r.HangfireJobId == hangfireJobId)
                    .ConfigureAwait(false);

                if (run is null) break;
                if (run.Status is RunStatus.Completed or RunStatus.Partial) break;

                run.Delete();
                await db.SaveChangesAsync().ConfigureAwait(false);

                await _broadcaster.PublishAsync(new WorkcastEvent
                {
                    Type    = WorkcastEvent.RunStatusChanged,
                    BoardId = boardId,
                    RunId   = run.Id,
                    Status  = "deleted",
                }).ConfigureAwait(false);
                break;
            }
        }
    }
}
