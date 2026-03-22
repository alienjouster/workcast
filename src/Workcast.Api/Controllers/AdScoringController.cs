using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Core.Interfaces;
using Workcast.Jobs;

namespace Workcast.Api.Controllers;

/// <summary>
/// Exposes resume scoring for individual job ads.
/// Scoring runs as a Hangfire background job and notifies the client via SSE when complete.
/// </summary>
[ApiController]
[Route("api/job-ads/{adId:guid}/scoring")]
public sealed class AdScoringController : ControllerBase
{
    private readonly IAdScoringRepository _scoringRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>Initializes a new instance of <see cref="AdScoringController"/>.</summary>
    public AdScoringController(
        IAdScoringRepository scoringRepository,
        ISettingsRepository settingsRepository,
        IBackgroundJobClient backgroundJobClient)
    {
        _scoringRepository = scoringRepository;
        _settingsRepository = settingsRepository;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>Returns the latest scoring result for the given job ad, or 404 if none exists.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AdScoringResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid adId, CancellationToken ct)
    {
        var scoring = await _scoringRepository.GetByAdIdAsync(adId, ct);
        if (scoring is null) return NotFound();
        return Ok(scoring.ToResponse());
    }

    /// <summary>
    /// Enqueues a scoring job for the given job ad and returns 202 Accepted immediately.
    /// The result will be delivered via SSE (<c>scoringCompleted</c> event) when done.
    /// Returns 422 if no resume has been uploaded.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RunAsync(Guid adId, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "No resume uploaded",
                Detail = "Upload a resume in Settings before running scoring.",
            });
        }

        _backgroundJobClient.Enqueue<AdScoringJob>(j => j.ExecuteAsync(adId, CancellationToken.None));
        return Accepted();
    }
}
