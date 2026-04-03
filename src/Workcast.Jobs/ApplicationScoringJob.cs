using Hangfire;
using Microsoft.Extensions.Logging;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that scores a user's resume against a specific application's job ad.
/// Delegates the scoring pipeline to <see cref="ScoringPipeline"/> and handles the
/// application-specific aftermath: updating the scoring snapshot on the <c>Application</c> entity
/// and firing an <c>applicationScoringCompleted</c> SSE event so the UI updates immediately.
/// </summary>
public sealed class ApplicationScoringJob
{
    private readonly AppDbContext _dbContext;
    private readonly IEventBroadcaster _broadcaster;
    private readonly ScoringPipeline _pipeline;
    private readonly ILogger<ApplicationScoringJob> _logger;

    /// <summary>Initialises a new instance of <see cref="ApplicationScoringJob"/>.</summary>
    public ApplicationScoringJob(
        AppDbContext dbContext,
        IEventBroadcaster broadcaster,
        ScoringPipeline pipeline,
        ILogger<ApplicationScoringJob> logger)
    {
        _dbContext = dbContext;
        _broadcaster = broadcaster;
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scoring pipeline for the given application:
    /// fetches the application → runs the pipeline → updates the scoring snapshot → fires SSE event.
    /// </summary>
    /// <param name="applicationId">The ID of the application to score.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [Queue("critical")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid applicationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting scoring for application {ApplicationId}", applicationId);

        var application = await _dbContext.Applications
            .FindAsync(new object[] { applicationId }, ct)
            .ConfigureAwait(false);

        if (application is null)
        {
            _logger.LogWarning("Application {ApplicationId} not found — scoring aborted", applicationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(application.JobAdContent))
        {
            _logger.LogWarning("Application {ApplicationId} has no job ad content — scoring aborted", applicationId);
            application.SetScoringFailed("Job Ad Detail is missing. Open the Job Ad tab and fetch the content before scoring.");
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await _broadcaster.PublishAsync(new WorkcastEvent
            {
                Type          = WorkcastEvent.ApplicationScoringCompleted,
                ApplicationId = applicationId,
            }).ConfigureAwait(false);
            return;
        }

        var outcome = await _pipeline.RunWithContentAsync(application.JobAdContent, ct).ConfigureAwait(false);

        if (outcome.Succeeded)
        {
            application.UpdateScoring(
                outcome.OverallScore,
                DateTimeOffset.UtcNow,
                outcome.Summary,
                outcome.Recommendation,
                outcome.Requirements);
            application.ClearScoringPending();

            _logger.LogInformation(
                "Scoring completed for application {ApplicationId}. Overall score: {Score:F1}",
                applicationId, outcome.OverallScore);
        }
        else
        {
            application.SetScoringFailed(outcome.Error!);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        await _broadcaster.PublishAsync(new WorkcastEvent
        {
            Type          = WorkcastEvent.ApplicationScoringCompleted,
            ApplicationId = applicationId,
        }).ConfigureAwait(false);
    }
}
