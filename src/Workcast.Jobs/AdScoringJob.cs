using Hangfire;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that scores a user's resume against a specific job ad.
/// Delegates the scoring pipeline to <see cref="ScoringPipeline"/> and handles the
/// ad-specific aftermath: persisting to <see cref="IAdScoringRepository"/> and firing
/// a <c>scoringCompleted</c> SSE event so the UI updates immediately.
/// </summary>
public sealed class AdScoringJob
{
    private readonly AppDbContext _dbContext;
    private readonly IAdScoringRepository _scoringRepository;
    private readonly IEventBroadcaster _broadcaster;
    private readonly ScoringPipeline _pipeline;
    private readonly ILogger<AdScoringJob> _logger;

    /// <summary>Initialises a new instance of <see cref="AdScoringJob"/>.</summary>
    public AdScoringJob(
        AppDbContext dbContext,
        IAdScoringRepository scoringRepository,
        IEventBroadcaster broadcaster,
        ScoringPipeline pipeline,
        ILogger<AdScoringJob> logger)
    {
        _dbContext = dbContext;
        _scoringRepository = scoringRepository;
        _broadcaster = broadcaster;
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scoring pipeline for the given job ad:
    /// fetches the ad → runs the pipeline → saves result → fires SSE event.
    /// </summary>
    /// <param name="adId">The ID of the job ad to score.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [Queue("critical")]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(Guid adId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting scoring for job ad {AdId}", adId);

        var ad = await _dbContext.JobAds.FindAsync(new object[] { adId }, ct).ConfigureAwait(false);
        if (ad is null)
        {
            _logger.LogWarning("Job ad {AdId} not found — scoring aborted", adId);
            return;
        }

        var outcome = await _pipeline.RunAsync(ad.Url, ct).ConfigureAwait(false);

        if (outcome.Succeeded)
        {
            var scoring = AdScoring.Create(
                adId,
                outcome.OverallScore,
                outcome.Summary!,
                outcome.Recommendation!,
                outcome.Requirements);

            await _scoringRepository.UpsertAsync(scoring, ct).ConfigureAwait(false);
            ad.ClearScoringPending();

            _logger.LogInformation(
                "Scoring completed for job ad {AdId}. Overall score: {Score:F1}",
                adId, outcome.OverallScore);
        }
        else
        {
            ad.SetScoringFailed(outcome.Error!);
        }

        await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        await _broadcaster.PublishAsync(new WorkcastEvent
        {
            Type = WorkcastEvent.ScoringCompleted,
            AdId = adId,
        }).ConfigureAwait(false);
    }
}
