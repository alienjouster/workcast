using Hangfire;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Workcast.Infrastructure.Observability;

/// <summary>
/// Background service that periodically reads Hangfire job statistics and exposes them
/// as Prometheus gauges. Runs every 15 seconds to keep metrics fresh.
/// </summary>
internal sealed class HangfireMetricsService : BackgroundService
{
    private static readonly Gauge Enqueued = Metrics.CreateGauge(
        "hangfire_jobs_enqueued", "Number of jobs currently waiting in a queue.");

    private static readonly Gauge Scheduled = Metrics.CreateGauge(
        "hangfire_jobs_scheduled", "Number of jobs scheduled to run in the future.");

    private static readonly Gauge Processing = Metrics.CreateGauge(
        "hangfire_jobs_processing", "Number of jobs currently being processed.");

    private static readonly Gauge Succeeded = Metrics.CreateGauge(
        "hangfire_jobs_succeeded", "Cumulative number of jobs that have succeeded.");

    private static readonly Gauge Failed = Metrics.CreateGauge(
        "hangfire_jobs_failed", "Number of jobs that are in a failed state.");

    private static readonly Gauge Recurring = Metrics.CreateGauge(
        "hangfire_jobs_recurring", "Number of registered recurring jobs.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var api = JobStorage.Current.GetMonitoringApi();
                var stats = api.GetStatistics();

                Enqueued.Set(stats.Enqueued);
                Scheduled.Set(stats.Scheduled);
                Processing.Set(stats.Processing);
                Succeeded.Set(stats.Succeeded);
                Failed.Set(stats.Failed);
                Recurring.Set(stats.Recurring);
            }
            catch
            {
                // Hangfire storage may not be initialised yet on the first tick — skip silently.
            }
        }
    }
}
