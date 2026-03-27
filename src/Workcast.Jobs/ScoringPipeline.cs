using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;

namespace Workcast.Jobs;

/// <summary>
/// Shared scoring pipeline used by both <see cref="AdScoringJob"/> and <see cref="ApplicationScoringJob"/>.
/// Encapsulates: resume validation, Playwright page rendering, content length check,
/// Claude AI scoring call, and requirements mapping.
/// </summary>
public sealed class ScoringPipeline
{
    private const int MinPageTextLength = 250;

    private readonly IAiProvider _aiProvider;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IScraperEngine _scraperEngine;
    private readonly ILogger<ScoringPipeline> _logger;

    /// <summary>Initialises a new instance of <see cref="ScoringPipeline"/>.</summary>
    public ScoringPipeline(
        IAiProvider aiProvider,
        ISettingsRepository settingsRepository,
        IScraperEngine scraperEngine,
        ILogger<ScoringPipeline> logger)
    {
        _aiProvider = aiProvider;
        _settingsRepository = settingsRepository;
        _scraperEngine = scraperEngine;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full scoring pipeline for the given job ad URL.
    /// Validates the resume, renders the page via Playwright, calls Claude, and maps the result.
    /// </summary>
    /// <param name="url">The job ad URL to score against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ScoringPipelineOutcome"/> with score data on success, or an error message on failure.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Propagated to Hangfire when no resume has been uploaded — prevents a retry loop.
    /// </exception>
    public async Task<ScoringPipelineOutcome> RunAsync(string url, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);
        if (!settings.HasResume)
            throw new InvalidOperationException("Cannot score: no resume has been uploaded in Settings.");

        try
        {
            _logger.LogInformation("Rendering job ad page {Url}", url);
            var pageText = await _scraperEngine.RenderPageTextAsync(url, ct: ct).ConfigureAwait(false);

            if (pageText.Length < MinPageTextLength)
                throw new InvalidOperationException(
                    $"Job ad page rendered less than {MinPageTextLength} characters of text ({pageText.Length} chars). " +
                    "The page may require authentication, be geo-blocked, or be temporarily unavailable.");

            var result = await _aiProvider.ScoreAdAsync(
                settings.ResumeContent!,
                settings.ResumeContentType!,
                settings.ResumeFileName!,
                pageText,
                ct).ConfigureAwait(false);

            var requirements = result.Requirements.Select(r => new ScoringRequirement
            {
                Name       = r.Name,
                Category   = r.Category,
                IsOptional = r.IsOptional,
                Score      = r.Score,
                Notes      = r.Notes,
            }).ToList();

            return ScoringPipelineOutcome.Success(result.OverallScore, result.Summary, result.Recommendation, requirements);
        }
        catch (OperationCanceledException)
        {
            return ScoringPipelineOutcome.Failure("Scoring was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scoring pipeline failed for URL {Url}", url);
            return ScoringPipelineOutcome.Failure(ex.Message);
        }
    }
}

/// <summary>
/// Outcome returned by <see cref="ScoringPipeline.RunAsync"/>.
/// Either carries the full scoring result or an error message.
/// </summary>
public sealed record ScoringPipelineOutcome
{
    public bool Succeeded => Error is null;

    public double OverallScore { get; private init; }
    public string? Summary { get; private init; }
    public string? Recommendation { get; private init; }
    public List<ScoringRequirement> Requirements { get; private init; } = [];
    public string? Error { get; private init; }

    public static ScoringPipelineOutcome Success(
        double overallScore,
        string? summary,
        string? recommendation,
        List<ScoringRequirement> requirements)
        => new()
        {
            OverallScore   = overallScore,
            Summary        = summary,
            Recommendation = recommendation,
            Requirements   = requirements,
        };

    public static ScoringPipelineOutcome Failure(string error) => new() { Error = error };
}
