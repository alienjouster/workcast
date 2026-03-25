using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that scores a user's resume against a specific job ad.
/// Renders the job ad detail page via Playwright (handles JS-heavy SPAs), sends it
/// alongside the resume to Claude, and persists the structured scoring result.
/// Fires a <c>scoringCompleted</c> SSE event on completion so the UI updates immediately.
/// </summary>
public sealed class AdScoringJob
{
    private readonly AppDbContext _dbContext;
    private readonly IAiProvider _aiProvider;
    private readonly IAdScoringRepository _scoringRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IEventBroadcaster _broadcaster;
    private readonly IScraperEngine _scraperEngine;
    private readonly ILogger<AdScoringJob> _logger;

    /// <summary>Initialises a new instance of <see cref="AdScoringJob"/>.</summary>
    public AdScoringJob(
        AppDbContext dbContext,
        IAiProvider aiProvider,
        IAdScoringRepository scoringRepository,
        ISettingsRepository settingsRepository,
        IEventBroadcaster broadcaster,
        IScraperEngine scraperEngine,
        ILogger<AdScoringJob> logger)
    {
        _dbContext = dbContext;
        _aiProvider = aiProvider;
        _scoringRepository = scoringRepository;
        _settingsRepository = settingsRepository;
        _broadcaster = broadcaster;
        _scraperEngine = scraperEngine;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scoring pipeline for the given job ad:
    /// fetches the ad page → calls Claude → saves result → fires SSE event.
    /// On failure the job is marked failed and the error propagates to Hangfire.
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

        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);
        if (!settings.HasResume)
            throw new InvalidOperationException("Cannot score: no resume has been uploaded in Settings.");

        const int MinPageTextLength = 250;
        string? scoringError = null;

        try
        {
            // Render the job ad page via Playwright and extract visible text directly.
            // Using innerText avoids the HTML-stripping heuristics in HtmlCleaningService
            // and guarantees zero HTML markup reaches the scoring prompt.
            _logger.LogInformation("Rendering job ad page {Url}", ad.Url);
            var pageText = await _scraperEngine.RenderPageTextAsync(ad.Url, ct: ct).ConfigureAwait(false);

            if (pageText.Length < MinPageTextLength)
                throw new InvalidOperationException(
                    $"Job ad page rendered less than {MinPageTextLength} characters of text ({pageText.Length} chars). " +
                    "The page may require authentication, be geo-blocked, or be temporarily unavailable.");

            // Call Claude for scoring.
            var result = await _aiProvider.ScoreAdAsync(
                settings.ResumeContent!,
                settings.ResumeContentType!,
                settings.ResumeFileName!,
                pageText,
                ct).ConfigureAwait(false);

            // Persist — replaces any previous scoring for this ad.
            var scoring = AdScoring.Create(
                adId,
                result.OverallScore,
                result.Summary,
                result.Recommendation,
                result.Requirements.Select(r => new ScoringRequirement
                {
                    Name = r.Name,
                    Category = r.Category,
                    IsOptional = r.IsOptional,
                    Score = r.Score,
                    Notes = r.Notes,
                }).ToList());

            await _scoringRepository.UpsertAsync(scoring, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Scoring completed for job ad {AdId}. Overall score: {Score:F1}",
                adId, result.OverallScore);
        }
        catch (OperationCanceledException)
        {
            scoringError = "Scoring was cancelled.";
        }
        catch (Exception ex)
        {
            scoringError = ex.Message;
            _logger.LogError(ex, "Scoring failed for job ad {AdId}", adId);
        }
        finally
        {
            // Always update the ad so the UI reflects the outcome.
            if (scoringError is not null)
                ad.SetScoringFailed(scoringError);
            else
                ad.ClearScoringPending();

            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            await _broadcaster.PublishAsync(new WorkcastEvent
            {
                Type = WorkcastEvent.ScoringCompleted,
                AdId = adId,
            }).ConfigureAwait(false);
        }
    }
}
