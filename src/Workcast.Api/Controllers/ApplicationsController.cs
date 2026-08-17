using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Requests;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.GoogleDrive;
using Workcast.Infrastructure.Persistence;
using Workcast.Jobs;
using Workcast.Core.Events;

namespace Workcast.Api.Controllers;

/// <summary>
/// Manages user job application files.
/// Each application is created from a job ad and carries a snapshot of the ad and any
/// scoring data at the time of creation, so it persists independently of the source ad.
/// </summary>
[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private const string ERROR_TYPE_BASE = "https://workcast.local/errors/";

    private const int MinJobAdContentLength = 250;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private readonly AppDbContext _db;
    private readonly IScraperEngine _scraperEngine;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IAiProvider _aiProvider;
    private readonly IInterviewDrillRepository _drillRepository;
    private readonly IGoogleDriveService _googleDriveService;

    /// <summary>Initializes a new instance of <see cref="ApplicationsController"/>.</summary>
    public ApplicationsController(
        AppDbContext db,
        IScraperEngine scraperEngine,
        ISettingsRepository settingsRepository,
        IBackgroundJobClient backgroundJobClient,
        IAiProvider aiProvider,
        IInterviewDrillRepository drillRepository,
        IGoogleDriveService googleDriveService)
    {
        _db = db;
        _scraperEngine = scraperEngine;
        _settingsRepository = settingsRepository;
        _backgroundJobClient = backgroundJobClient;
        _aiProvider = aiProvider;
        _drillRepository = drillRepository;
        _googleDriveService = googleDriveService;
    }

    /// <summary>
    /// Creates a new application from a job ad, copying all available job ad and scoring data.
    /// If an application already exists for the given job ad, the existing application is returned
    /// with HTTP 200 instead of creating a duplicate.
    /// Returns HTTP 201 for a newly created application.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateApplicationRequest request,
        CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { request.JobAdId }, ct);
        if (ad is null) return NotFound();

        // Return existing application if one already exists for this job ad.
        var existing = await _db.Applications
            .FirstOrDefaultAsync(a => a.JobAdId == request.JobAdId, ct);

        if (existing is not null)
            return Ok(existing.ToResponse());

        var scoring = await _db.AdScorings
            .FirstOrDefaultAsync(s => s.JobAdId == request.JobAdId, ct);

        var application = Application.CreateFromJobAd(ad, scoring);
        _db.Applications.Add(application);
        await _db.SaveChangesAsync(ct);

        // Fetch and store the full job ad page content.
        var content = await FetchJobAdContentAsync(ad.Url, ct);
        if (content is not null)
        {
            application.UpdateJobAdContent(content);
            await _db.SaveChangesAsync(ct);
        }

        return Created($"/api/applications/{application.Id}", application.ToResponse());
    }

    /// <summary>
    /// Returns a paginated list of applications with optional filtering.
    /// Ordered by <c>CreatedAt DESC</c>, then <c>Id DESC</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string[]? titles,
        [FromQuery] string[]? excludeTitles,
        [FromQuery] string[]? locations,
        [FromQuery] string[]? excludeLocations,
        [FromQuery] string[]? companies,
        [FromQuery] string[]? excludeCompanies,
        [FromQuery] double? minScore,
        [FromQuery] bool trashed = false,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        int? cursorStatusPriority = null;
        DateTimeOffset? cursorCreatedAt = null;
        Guid? cursorId = null;

        if (!string.IsNullOrEmpty(cursor))
        {
            var decoded = DecodeCursor(cursor);
            if (decoded is null)
            {
                return Problem(
                    type: $"{ERROR_TYPE_BASE}invalid-cursor",
                    title: "Invalid Cursor",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "The provided cursor value is invalid or corrupted.");
            }
            (cursorStatusPriority, cursorCreatedAt, cursorId) = decoded.Value;
        }

        var query = _db.Applications.AsQueryable()
            .Where(a => a.IsTrashed == trashed);

        if (titles?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Title, titles);
        if (excludeTitles?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Title, excludeTitles);
        if (locations?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Location, locations);
        if (excludeLocations?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Location, excludeLocations);
        if (companies?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Company, companies);
        if (excludeCompanies?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Company, excludeCompanies);
        if (minScore is not null)
            query = query.Where(a => a.OverallScore != null && a.OverallScore >= minScore);

        var totalCount = await query.CountAsync(ct);

        if (cursorStatusPriority is not null && cursorCreatedAt is not null && cursorId is not null)
        {
            var cPriority = cursorStatusPriority.Value;
            var cAt = cursorCreatedAt.Value;
            var cId = cursorId.Value;
            query = query.Where(a =>
                (a.Status == ApplicationStatus.ToApply ? 0 :
                 a.Status == ApplicationStatus.Interviewing ? 1 :
                 a.Status == ApplicationStatus.Applied ? 2 : 3) > cPriority
                || ((a.Status == ApplicationStatus.ToApply ? 0 :
                     a.Status == ApplicationStatus.Interviewing ? 1 :
                     a.Status == ApplicationStatus.Applied ? 2 : 3) == cPriority
                    && a.CreatedAt < cAt)
                || ((a.Status == ApplicationStatus.ToApply ? 0 :
                     a.Status == ApplicationStatus.Interviewing ? 1 :
                     a.Status == ApplicationStatus.Applied ? 2 : 3) == cPriority
                    && a.CreatedAt == cAt
                    && a.Id.CompareTo(cId) < 0));
        }

        var items = await query
            .OrderBy(a =>
                a.Status == ApplicationStatus.ToApply ? 0 :
                a.Status == ApplicationStatus.Interviewing ? 1 :
                a.Status == ApplicationStatus.Applied ? 2 : 3)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(limit);
            var last = items[^1];
            nextCursor = EncodeCursor(StatusSortPriority(last.Status), last.CreatedAt, last.Id);
        }

        return Ok(new PagedResponse<ApplicationResponse>
        {
            Items = items.Select(a => a.ToResponse()).ToList(),
            NextCursor = nextCursor,
            Count = items.Count,
            TotalCount = totalCount,
        });
    }

    /// <summary>
    /// Returns pre-computed statistics across all non-trashed applications.
    /// All aggregations are performed in-process after loading the full dataset.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApplicationStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatsAsync(CancellationToken ct)
    {
        var apps = await _db.Applications
            .Where(a => !a.IsTrashed)
            .AsNoTracking()
            .ToListAsync(ct);

        var appIds = apps.Select(a => a.Id).ToHashSet();
        var stepCounts = await _db.InterviewSteps
            .Where(s => appIds.Contains(s.ApplicationId))
            .GroupBy(s => s.ApplicationId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var stepsByApp = stepCounts.ToDictionary(x => x.Id, x => x.Count);

        var totalApplications = apps.Count;
        var totalSubmitted    = apps.Count(a => a.Status != ApplicationStatus.ToApply);
        var totalInterviewed  = apps.Count(a => a.StatusHistory.Any(e => e.Status == ApplicationStatus.Interviewing));
        var totalHired        = apps.Count(a => a.Status == ApplicationStatus.ClosedHired);

        double? interviewHitRatio = totalSubmitted == 0 ? null
            : (double)totalInterviewed / totalSubmitted * 100;

        var daysToApply = apps
            .Select(a =>
            {
                var applied = a.StatusHistory.FirstOrDefault(e => e.Status == ApplicationStatus.Applied);
                return applied is null ? (double?)null : (applied.AchievedAt - a.ScrapedAt).TotalDays;
            })
            .Where(d => d is >= 0)
            .Select(d => d!.Value)
            .ToList();
        double? averageDaysToApply = daysToApply.Count == 0 ? null : daysToApply.Average();

        var daysToInterview = apps
            .Select(a =>
            {
                var applied      = a.StatusHistory.FirstOrDefault(e => e.Status == ApplicationStatus.Applied);
                var interviewed  = a.StatusHistory.FirstOrDefault(e => e.Status == ApplicationStatus.Interviewing);
                return applied is null || interviewed is null
                    ? (double?)null
                    : (interviewed.AchievedAt - applied.AchievedAt).TotalDays;
            })
            .Where(d => d is >= 0)
            .Select(d => d!.Value)
            .ToList();
        double? averageDaysToInterview = daysToInterview.Count == 0 ? null : daysToInterview.Average();

        var appsWithSteps = stepsByApp.Values.Where(c => c >= 1).ToList();
        double? averageInterviewSteps = appsWithSteps.Count == 0 ? null
            : appsWithSteps.Average(c => (double)c);

        var scored = apps.Where(a => a.OverallScore is not null).Select(a => a.OverallScore!.Value).ToList();
        double? averageScore = scored.Count == 0 ? null : scored.Average();

        var scoredInterviewed = apps
            .Where(a => a.OverallScore is not null &&
                        a.StatusHistory.Any(e => e.Status == ApplicationStatus.Interviewing))
            .Select(a => a.OverallScore!.Value)
            .ToList();
        double? averageScoreInterviewed = scoredInterviewed.Count == 0 ? null : scoredInterviewed.Average();

        var perStatus = apps
            .GroupBy(a => a.Status.ToString())
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var months = Enumerable.Range(0, 6)
            .Select(i => now.AddMonths(-i))
            .Select(d => $"{d.Year:D4}-{d.Month:D2}")
            .OrderBy(m => m)
            .ToList();
        var countsByMonth = apps
            .GroupBy(a => $"{a.CreatedAt.Year:D4}-{a.CreatedAt.Month:D2}")
            .ToDictionary(g => g.Key, g => g.Count());
        var perMonth = months
            .Select(m => new MonthlyApplicationCountDto(m, countsByMonth.TryGetValue(m, out var c) ? c : 0))
            .ToList();

        return Ok(new ApplicationStatsResponse
        {
            TotalApplications       = totalApplications,
            TotalSubmitted          = totalSubmitted,
            TotalInterviewed        = totalInterviewed,
            TotalHired              = totalHired,
            InterviewHitRatio       = interviewHitRatio,
            AverageDaysToApply      = averageDaysToApply,
            AverageDaysToInterview  = averageDaysToInterview,
            AverageInterviewSteps   = averageInterviewSteps,
            AverageScore            = averageScore,
            AverageScoreInterviewed = averageScoreInterviewed,
            ApplicationsPerStatus   = perStatus,
            ApplicationsPerMonth    = perMonth,
        });
    }

    /// <summary>Returns a single application by ID, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();
        return Ok(application.ToResponse());
    }

    /// <summary>Returns distinct title values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-titles")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctTitlesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Title != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Title, [q]);
        var results = await query
            .Select(a => a.Title!)
            .Distinct()
            .OrderBy(t => t)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    /// <summary>Returns distinct location values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-locations")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctLocationsAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Location != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Location, [q]);
        var results = await query
            .Select(a => a.Location!)
            .Distinct()
            .OrderBy(l => l)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    /// <summary>Returns distinct company values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-companies")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctCompaniesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Company != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Company, [q]);
        var results = await query
            .Select(a => a.Company!)
            .Distinct()
            .OrderBy(c => c)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    /// <summary>Moves an application to the trash bin.</summary>
    [HttpPatch("{id:guid}/trash")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrashAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.Trash();
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Restores an application from the trash bin.</summary>
    [HttpPatch("{id:guid}/restore")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.Restore();
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Permanently deletes an application. This action cannot be undone.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Updates the workflow status of an application and records the transition date.
    /// If <c>achievedAt</c> is omitted, the current UTC time is used for new history entries;
    /// existing entries keep their original date unless <c>achievedAt</c> is explicitly provided.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatusAsync(
        Guid id,
        [FromBody] UpdateApplicationStatusRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.UpdateStatus(request.Status, request.AchievedAt);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>
    /// Overwrites the recorded date for a specific status in the application's history.
    /// Returns 404 if the application is not found; 422 if the given status has not been reached yet.
    /// </summary>
    [HttpPatch("{id:guid}/status/date")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatusDateAsync(
        Guid id,
        [FromBody] UpdateStatusDateRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        if (!application.StatusHistory.Any(e => e.Status == request.Status))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Status not reached",
                Detail = $"The status '{request.Status}' has not been recorded for this application.",
            });
        }

        application.UpdateStatusDate(request.Status, request.AchievedAt);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Sets or clears the recorded posting date of the job ad for an application.</summary>
    [HttpPatch("{id:guid}/posted-at")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePostedAtAsync(
        Guid id,
        [FromBody] UpdatePostedAtRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.UpdatePostedAt(request.PostedAt);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Updates the recorded scrape date for an application.</summary>
    [HttpPatch("{id:guid}/scraped-at")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateScrapedAtAsync(
        Guid id,
        [FromBody] UpdateScrapedAtRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.UpdateScrapedAt(request.ScrapedAt);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Updates the stored job ad page content for an application.</summary>
    [HttpPatch("{id:guid}/job-ad-content")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateJobAdContentAsync(
        Guid id,
        [FromBody] UpdateJobAdContentRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.UpdateJobAdContent(request.Content);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>
    /// Enqueues a scoring job for the given application and returns 202 Accepted immediately.
    /// The result will be delivered via SSE (<c>applicationScoringCompleted</c> event) when done.
    /// Returns 422 if no resume has been uploaded or if the job ad detail is missing.
    /// </summary>
    [HttpPost("{id:guid}/scoring")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RunScoringAsync(Guid id, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume uploaded",
                Detail = "Upload a resume in Settings before running scoring.",
            });
        }

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        if (string.IsNullOrWhiteSpace(application.JobAdContent))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No job ad detail",
                Detail = "Fetch the job ad detail from the Job Ad tab before running scoring.",
            });
        }

        application.SetScoringPending();
        await _db.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<ApplicationScoringJob>(j => j.ExecuteAsync(id, CancellationToken.None));
        return Accepted();
    }

    /// <summary>
    /// Clears a stuck <c>isScoringPending</c> flag, allowing the user to re-trigger scoring.
    /// Safe to call even if scoring is not currently pending.
    /// </summary>
    [HttpDelete("{id:guid}/scoring")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelScoringAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.ClearScoringPending();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Enqueues a Hangfire background job to generate a tailored HTML resume for the given application.
    /// Requires: a resume uploaded in Settings, an HTML template uploaded in Settings,
    /// and scoring data present on the application.
    /// Returns 202 Accepted immediately; the UI is updated via SSE when the job completes.
    /// </summary>
    [HttpPost("{id:guid}/resume/generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateResumeAsync(Guid id, [FromBody] GenerateResumeRequest? request, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);

        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume uploaded",
                Detail = "Upload a resume (content) in Settings before generating a custom resume.",
            });
        }

        if (!settings.HasResumeTemplate)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume template uploaded",
                Detail = "Upload an HTML resume template in Settings before generating a custom resume.",
            });
        }

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        if (application.OverallScore is null)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No scoring data",
                Detail = "Run scoring on this application before generating a custom resume.",
            });
        }

        application.SetResumeGenerationPending();
        await _db.SaveChangesAsync(ct);

        var level = request?.OptimizationLevel ?? ResumeOptimizationLevel.None;
        _backgroundJobClient.Enqueue<ApplicationResumeGenerationJob>(j => j.ExecuteAsync(id, level, CancellationToken.None));

        return Accepted();
    }

    /// <summary>
    /// Saves a manual edit of the resume as a new version.
    /// The new version inherits the model name from the highest existing version.
    /// OptimizationLevel is inherited from the prior version and IsManualEdit is true for all versions created here.
    /// </summary>
    [HttpPatch("{id:guid}/resume/latest")]
    [ProducesResponseType(typeof(GeneratedResumeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLatestResumeAsync(
        Guid id,
        [FromBody] UpdateGeneratedResumeRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var latest = await _db.GeneratedResumes
            .Where(r => r.ApplicationId == id)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return NotFound();

        var newVersion = GeneratedResume.Create(
            id,
            request.HtmlContent,
            latest.ModelUsed,
            latest.VersionNumber + 1,
            optimizationLevel: latest.OptimizationLevel,
            isManualEdit: true);

        _db.GeneratedResumes.Add(newVersion);
        await _db.SaveChangesAsync(ct);

        return Ok(MapResumeToResponse(newVersion));
    }

    /// <summary>
    /// Returns the most recently generated HTML resume for the given application,
    /// or 404 if no resume has been generated yet.
    /// </summary>
    [HttpGet("{id:guid}/resume/latest")]
    [ProducesResponseType(typeof(GeneratedResumeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestResumeAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var latest = await _db.GeneratedResumes
            .Where(r => r.ApplicationId == id)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return NotFound();

        return Ok(MapResumeToResponse(latest));
    }

    /// <summary>
    /// Returns all versions of the generated resume for the given application,
    /// ordered by version number descending (latest first).
    /// </summary>
    [HttpGet("{id:guid}/resume/versions")]
    [ProducesResponseType(typeof(IList<GeneratedResumeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListResumeVersionsAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var versions = await _db.GeneratedResumes
            .Where(r => r.ApplicationId == id)
            .OrderByDescending(r => r.VersionNumber)
            .ToListAsync(ct);

        return Ok(versions.Select(MapResumeToResponse).ToList());
    }

    /// <summary>
    /// Permanently deletes a specific resume version. This cannot be undone.
    /// Returns 404 if the version does not belong to the given application.
    /// </summary>
    [HttpDelete("{id:guid}/resume/versions/{versionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResumeVersionAsync(Guid id, Guid versionId, CancellationToken ct)
    {
        var version = await _db.GeneratedResumes
            .FirstOrDefaultAsync(r => r.Id == versionId && r.ApplicationId == id, ct);

        if (version is null) return NotFound();

        _db.GeneratedResumes.Remove(version);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Enqueues a Hangfire background job to generate an HTML application letter for the given application.
    /// Requires: a resume uploaded in Settings and scoring data present on the application.
    /// Returns 202 Accepted immediately; the UI is updated via SSE when the job completes.
    /// </summary>
    [HttpPost("{id:guid}/letter/generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateLetterAsync(Guid id, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);

        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume uploaded",
                Detail = "Upload a resume (content) in Settings before generating an application letter.",
            });
        }

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        if (application.OverallScore is null)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No scoring data",
                Detail = "Run scoring on this application before generating an application letter.",
            });
        }

        application.SetLetterGenerationPending();
        await _db.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<ApplicationLetterGenerationJob>(j => j.ExecuteAsync(id, CancellationToken.None));

        return Accepted();
    }

    /// <summary>
    /// Saves a manual edit of the letter as a new version.
    /// The new version inherits the model name from the highest existing version.
    /// IsManualEdit is true for all versions created here.
    /// </summary>
    [HttpPatch("{id:guid}/letter/latest")]
    [ProducesResponseType(typeof(GeneratedLetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLatestLetterAsync(
        Guid id,
        [FromBody] UpdateGeneratedLetterRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var latest = await _db.GeneratedLetters
            .Where(l => l.ApplicationId == id)
            .OrderByDescending(l => l.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return NotFound();

        var newVersion = GeneratedLetter.Create(
            id,
            request.HtmlContent,
            latest.ModelUsed,
            latest.VersionNumber + 1,
            isManualEdit: true);

        _db.GeneratedLetters.Add(newVersion);
        await _db.SaveChangesAsync(ct);

        return Ok(MapLetterToResponse(newVersion));
    }

    /// <summary>
    /// Returns the most recently generated HTML application letter for the given application,
    /// or 404 if no letter has been generated yet.
    /// </summary>
    [HttpGet("{id:guid}/letter/latest")]
    [ProducesResponseType(typeof(GeneratedLetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestLetterAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var latest = await _db.GeneratedLetters
            .Where(l => l.ApplicationId == id)
            .OrderByDescending(l => l.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return NotFound();

        return Ok(MapLetterToResponse(latest));
    }

    /// <summary>
    /// Returns all versions of the generated letter for the given application,
    /// ordered by version number descending (latest first).
    /// </summary>
    [HttpGet("{id:guid}/letter/versions")]
    [ProducesResponseType(typeof(IList<GeneratedLetterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListLetterVersionsAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var versions = await _db.GeneratedLetters
            .Where(l => l.ApplicationId == id)
            .OrderByDescending(l => l.VersionNumber)
            .ToListAsync(ct);

        return Ok(versions.Select(MapLetterToResponse).ToList());
    }

    /// <summary>
    /// Permanently deletes a specific letter version. This cannot be undone.
    /// Returns 404 if the version does not belong to the given application.
    /// </summary>
    [HttpDelete("{id:guid}/letter/versions/{versionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLetterVersionAsync(Guid id, Guid versionId, CancellationToken ct)
    {
        var version = await _db.GeneratedLetters
            .FirstOrDefaultAsync(l => l.Id == versionId && l.ApplicationId == id, ct);

        if (version is null) return NotFound();

        _db.GeneratedLetters.Remove(version);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Interview drill ───────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues a Hangfire background job to generate an interview drill plan for the given application.
    /// Requires a resume uploaded in Settings.
    /// Returns 202 Accepted immediately; the UI is updated via SSE when the job completes.
    /// </summary>
    [HttpPost("{id:guid}/interview-drill/generate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateInterviewDrillAsync(Guid id, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);

        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume uploaded",
                Detail = "Upload a resume in Settings before generating an interview drill.",
            });
        }

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        if (application.OverallScore is null)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No scoring data",
                Detail = "Run scoring on this application before generating an interview drill.",
            });
        }

        application.SetInterviewDrillPending();
        await _db.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<InterviewDrillJob>(j => j.ExecuteAsync(id, CancellationToken.None));
        return Accepted();
    }

    /// <summary>
    /// Clears a stuck <c>isInterviewDrillPending</c> flag, allowing the user to re-trigger generation.
    /// </summary>
    [HttpDelete("{id:guid}/interview-drill/generate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelInterviewDrillAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.ClearInterviewDrillPending();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Returns the current interview drill plan for the given application, or 404 if none exists.
    /// </summary>
    [HttpGet("{id:guid}/interview-drill")]
    [ProducesResponseType(typeof(InterviewDrillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInterviewDrillAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var plan = await _drillRepository.GetByApplicationIdAsync(id, ct);
        if (plan is null) return NotFound();

        return Ok(MapDrillToResponse(plan));
    }

    /// <summary>
    /// Saves (or clears) the user's answer for a single drill question.
    /// Passing null or an empty string clears a previous answer.
    /// </summary>
    [HttpPut("{id:guid}/interview-drill/questions/{orderIndex:int}/answer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDrillAnswerAsync(
        Guid id,
        int orderIndex,
        [FromBody] SaveDrillAnswerRequest request,
        CancellationToken ct)
    {
        var found = await _drillRepository.SaveAnswerAsync(id, orderIndex, request.Answer, ct);
        return found ? NoContent() : NotFound();
    }

    /// <summary>
    /// Clears all saved answers on the current drill plan for the given application.
    /// </summary>
    [HttpDelete("{id:guid}/interview-drill/answers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearDrillAnswersAsync(Guid id, CancellationToken ct)
    {
        var found = await _drillRepository.ClearAnswersAsync(id, ct);
        return found ? NoContent() : NotFound();
    }

    /// <summary>
    /// Evaluates the candidate's answer for a single drill question using AI.
    /// Returns structured recruiter-perspective feedback: rating, assessment, and improvement tips.
    /// </summary>
    [HttpPost("{id:guid}/interview-drill/questions/{orderIndex:int}/evaluate")]
    [ProducesResponseType(typeof(InterviewAnswerEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EvaluateDrillAnswerAsync(
        Guid id,
        int orderIndex,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var plan = await _drillRepository.GetByApplicationIdAsync(id, ct);
        if (plan is null) return NotFound();

        var question = plan.Questions.FirstOrDefault(q => q.OrderIndex == orderIndex);
        if (question is null) return NotFound();

        if (string.IsNullOrWhiteSpace(question.Answer))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "No answer to evaluate",
                Detail = "Save an answer for this question before requesting evaluation.",
            });
        }

        var jobAdContent = application.JobAdContent;
        if (string.IsNullOrWhiteSpace(jobAdContent))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "No job ad content",
                Detail = "The application has no job ad content available for evaluation context.",
            });
        }

        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "No resume uploaded",
                Detail = "Upload a resume in Settings before using answer evaluation.",
            });
        }

        var result = await _aiProvider.EvaluateInterviewAnswerAsync(
            questionText:      question.Text,
            category:          question.Category,
            requirementName:   question.RequirementName,
            answer:            question.Answer,
            jobAdContent:      jobAdContent,
            resumeContent:     settings.ResumeContent!,
            resumeContentType: settings.ResumeContentType!,
            resumeFileName:    settings.ResumeFileName!,
            model:             settings.InterviewAnswerEvaluationModel,
            maxTokens:         settings.InterviewAnswerEvaluationMaxTokens,
            ct:                ct);

        return Ok(new InterviewAnswerEvaluationResponse(result.Rating, result.Feedback, result.Tips));
    }

    // ── Interview Steps ───────────────────────────────────────────────────────

    /// <summary>Returns all interview steps for an application, ordered by step number.</summary>
    [HttpGet("{id:guid}/interview-steps")]
    [ProducesResponseType(typeof(IList<InterviewStepResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInterviewStepsAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.Applications.AnyAsync(a => a.Id == id, ct);
        if (!exists) return NotFound();

        var steps = await _db.InterviewSteps
            .Where(s => s.ApplicationId == id)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        return Ok(steps.Select(s => s.ToResponse()).ToList());
    }

    /// <summary>Creates a new interview step for an application.</summary>
    [HttpPost("{id:guid}/interview-steps")]
    [ProducesResponseType(typeof(InterviewStepResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateInterviewStepAsync(
        Guid id,
        [FromBody] CreateInterviewStepRequest request,
        CancellationToken ct)
    {
        var exists = await _db.Applications.AnyAsync(a => a.Id == id, ct);
        if (!exists) return NotFound();

        var maxStep = await _db.InterviewSteps
            .Where(s => s.ApplicationId == id)
            .MaxAsync(s => (int?)s.StepNumber, ct) ?? 0;

        var date = request.Date is not null && DateOnly.TryParse(request.Date, out var d) ? d : (DateOnly?)null;
        var time = request.Time is not null && TimeOnly.TryParse(request.Time, out var t) ? t : (TimeOnly?)null;

        var step = InterviewStep.Create(
            applicationId:   id,
            stepNumber:      maxStep + 1,
            date:            date,
            time:            time,
            durationMinutes: request.DurationMinutes,
            timezone:        request.Timezone,
            isOnSite:        request.IsOnSite,
            remoteCallLink:  request.RemoteCallLink,
            interviewers:    request.Interviewers
                .Select(i => new InterviewStepInterviewer { Name = i.Name, JobFunction = i.JobFunction })
                .ToList(),
            notes:           request.Notes);

        _db.InterviewSteps.Add(step);
        await _db.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created, step.ToResponse());
    }

    /// <summary>Updates an existing interview step.</summary>
    [HttpPut("{id:guid}/interview-steps/{stepId:guid}")]
    [ProducesResponseType(typeof(InterviewStepResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInterviewStepAsync(
        Guid id,
        Guid stepId,
        [FromBody] UpdateInterviewStepRequest request,
        CancellationToken ct)
    {
        var step = await _db.InterviewSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ApplicationId == id, ct);
        if (step is null) return NotFound();

        var date = request.Date is not null && DateOnly.TryParse(request.Date, out var d) ? d : (DateOnly?)null;
        var time = request.Time is not null && TimeOnly.TryParse(request.Time, out var t) ? t : (TimeOnly?)null;

        step.Update(
            date:            date,
            time:            time,
            durationMinutes: request.DurationMinutes,
            timezone:        request.Timezone,
            isOnSite:        request.IsOnSite,
            remoteCallLink:  request.RemoteCallLink,
            interviewers:    request.Interviewers
                .Select(i => new InterviewStepInterviewer { Name = i.Name, JobFunction = i.JobFunction })
                .ToList(),
            notes:           request.Notes);

        await _db.SaveChangesAsync(ct);
        return Ok(step.ToResponse());
    }

    /// <summary>Deletes an interview step.</summary>
    [HttpDelete("{id:guid}/interview-steps/{stepId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInterviewStepAsync(
        Guid id,
        Guid stepId,
        CancellationToken ct)
    {
        var step = await _db.InterviewSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ApplicationId == id, ct);
        if (step is null) return NotFound();

        _db.InterviewSteps.Remove(step);

        // Renumber remaining steps so they stay contiguous (1, 2, 3, …).
        var remaining = await _db.InterviewSteps
            .Where(s => s.ApplicationId == id && s.Id != stepId)
            .OrderBy(s => s.StepNumber)
            .ToListAsync(ct);

        for (var i = 0; i < remaining.Count; i++)
            remaining[i].Renumber(i + 1);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Saves the application's job ad, latest resume, and latest letter to Google Drive.
    /// Creates a subfolder on first call; subsequent calls update the existing files.
    /// Returns 422 if Google Drive is not connected.
    /// </summary>
    [HttpPost("{id:guid}/save-to-drive")]
    [ProducesResponseType(typeof(SaveToDriveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SaveToDriveAsync(Guid id, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.IsGoogleDriveConnected)
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Google Drive not connected",
                Detail = "Connect Google Drive in Settings first.",
            });

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        var refreshToken = settings.GoogleDriveRefreshToken!;

        var baseFolderId = await _googleDriveService.EnsureBaseFolderAsync(
            refreshToken, settings.GoogleDriveBasePath, settings.GoogleDriveBaseFolderId, ct);
        if (baseFolderId != settings.GoogleDriveBaseFolderId)
        {
            settings.SetGoogleDriveBaseFolderId(baseFolderId);
            await _settingsRepository.SaveAsync(ct);
        }

        string appFolderId;
        if (application.GoogleDriveFolderId is not null)
        {
            appFolderId = application.GoogleDriveFolderId;
        }
        else
        {
            appFolderId = await _googleDriveService.CreateSubfolderAsync(
                refreshToken, baseFolderId, BuildSubfolderName(application), ct);
            application.SetGoogleDriveFolderId(appFolderId);
            await _db.SaveChangesAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(application.JobAdContent))
        {
            var plain = Regex.Replace(application.JobAdContent, @"<[^>]+>", "", RegexOptions.None, RegexTimeout).Trim();
            await _googleDriveService.UpsertFileAsync(refreshToken, appFolderId, "job-ad.txt", "text/plain", plain, ct);
        }

        var resume = await _db.GeneratedResumes
            .Where(r => r.ApplicationId == id)
            .OrderByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (resume is not null)
            await _googleDriveService.UpsertFileAsync(refreshToken, appFolderId, "resume.html", "text/html", resume.HtmlContent, ct);

        var letter = await _db.GeneratedLetters
            .Where(l => l.ApplicationId == id)
            .OrderByDescending(l => l.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (letter is not null)
            await _googleDriveService.UpsertFileAsync(refreshToken, appFolderId, "letter.html", "text/html", letter.HtmlContent, ct);

        return Ok(new SaveToDriveResponse(appFolderId, IGoogleDriveService.GetFolderWebViewLink(appFolderId)));
    }

    private static string BuildSubfolderName(Application app)
    {
        var date    = app.CreatedAt.ToString("yyyy-MM-dd");
        var company = SanitizeSegment(app.Company ?? "Unknown company");
        var title   = SanitizeSegment(app.Title   ?? "Unknown title");
        return $"{date} - {company} - {title}";
    }

    private static string SanitizeSegment(string value)
    {
        var s = Regex.Replace(value, @"[/\\:*?""<>|]", " ", RegexOptions.None, RegexTimeout);
        s = Regex.Replace(s, @"\s{2,}", " ", RegexOptions.None, RegexTimeout).Trim();
        return s.Length > 80 ? s[..80].TrimEnd() : s;
    }

    private static InterviewDrillResponse MapDrillToResponse(InterviewDrillPlan plan) =>
        new(
            plan.Id,
            plan.ApplicationId,
            plan.GeneratedAt,
            plan.ModelUsed,
            plan.Questions
                .Select(q => new InterviewQuestionDto(q.OrderIndex, q.Text, q.Category, q.RequirementName, q.Answer, q.AnsweredAt))
                .ToList());

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GeneratedLetterResponse MapLetterToResponse(GeneratedLetter l) =>
        new()
        {
            Id            = l.Id,
            ApplicationId = l.ApplicationId,
            VersionNumber = l.VersionNumber,
            HtmlContent   = l.HtmlContent,
            ModelUsed     = l.ModelUsed,
            GeneratedAt   = l.GeneratedAt,
            IsManualEdit  = l.IsManualEdit,
        };

    private static GeneratedResumeResponse MapResumeToResponse(GeneratedResume r) =>
        new()
        {
            Id                = r.Id,
            ApplicationId     = r.ApplicationId,
            VersionNumber     = r.VersionNumber,
            HtmlContent       = r.HtmlContent,
            ModelUsed         = r.ModelUsed,
            GeneratedAt       = r.GeneratedAt,
            OptimizationLevel = r.OptimizationLevel?.ToString(),
            IsManualEdit      = r.IsManualEdit,
        };

    /// <summary>
    /// Renders the job ad page via Playwright and returns sanitized HTML that preserves
    /// the visual structure (headings, bold, italic, lists, inline styles) while stripping
    /// scripts, navigation, images, and dangerous attributes.
    /// Returns null if rendering fails or the extracted text content is shorter than
    /// <see cref="MinJobAdContentLength"/>.
    /// </summary>
    private async Task<string?> FetchJobAdContentAsync(string url, CancellationToken ct)
    {
        try
        {
            var html = await _scraperEngine.RenderPageAsync(url, ct: ct);
            var sanitized = SanitizePageHtml(html);
            // Measure meaningful text length by stripping tags from the sanitized output.
            var textLength = Regex.Replace(sanitized, @"<[^>]+>", "", RegexOptions.None, RegexTimeout).Trim().Length;
            return textLength >= MinJobAdContentLength ? sanitized : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sanitizes raw page HTML for storage and display in the rich-text Job Ad Detail panel.
    /// Keeps structural and inline-styled markup; removes everything that is either dangerous
    /// (scripts, event handlers) or irrelevant to the job description (nav, images, forms).
    /// </summary>
    private static string SanitizePageHtml(string html)
    {
        // ── 1. Extract the most focused content region available ─────────────
        // Prefer <main> or <article> over the full <body> to skip page chrome.
        var content =
            TryExtractElement(html, "main") ??
            TryExtractElement(html, "article") ??
            TryExtractElement(html, "body") ??
            html;

        // ── 2. Remove blocks that are never useful for job-description display ─
        content = Regex.Replace(
            content,
            @"<(script|style|noscript|nav|header|footer|aside|form|iframe|svg|figure)[^>]*>[\s\S]*?</(script|style|noscript|nav|header|footer|aside|form|iframe|svg|figure)>",
            "",
            RegexOptions.IgnoreCase,
            RegexTimeout);

        // Remove self-closing / void elements that add no readable value.
        content = Regex.Replace(
            content,
            @"<(img|input|select|textarea|button|link|meta|canvas|video|audio|source|track|embed|object)[^>]*/?>",
            "",
            RegexOptions.IgnoreCase,
            RegexTimeout);

        // ── 3. Strip dangerous attributes; keep only safe ones ────────────────
        // Allow: style, href (filtered below), target, colspan, rowspan, align.
        content = Regex.Replace(
            content,
            @"\s(?:class|id|on\w+|data-\w+|aria-\w+|role|tabindex|name|action|method|enctype|autocomplete)[^=]*=(?:""[^""]*""|'[^']*'|[^\s>]*)",
            "",
            RegexOptions.IgnoreCase,
            RegexTimeout);

        // Remove javascript: hrefs.
        content = Regex.Replace(
            content,
            @"href\s*=\s*(?:""javascript:[^""]*""|'javascript:[^']*')",
            "",
            RegexOptions.IgnoreCase,
            RegexTimeout);

        // ── 4. Normalise whitespace ───────────────────────────────────────────
        content = Regex.Replace(content, @"[ \t]{2,}", " ", RegexOptions.None, RegexTimeout);
        content = Regex.Replace(content, @"(\s*\n\s*){3,}", "\n\n", RegexOptions.None, RegexTimeout);

        return content.Trim();
    }

    /// <summary>
    /// Extracts the inner content of the first occurrence of <paramref name="tag"/> in
    /// <paramref name="html"/>, or null if the tag is not present.
    /// </summary>
    private static string? TryExtractElement(string html, string tag)
    {
        var match = Regex.Match(
            html,
            $@"<{tag}(?:\s[^>]*)?>(?<content>[\s\S]*?)</{tag}>",
            RegexOptions.IgnoreCase,
            RegexTimeout);
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static IQueryable<Application> ApplyPartialMatchFilter(
        IQueryable<Application> query,
        Expression<Func<Application, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Application), "a");
        var prop = Expression.Property(param, ((MemberExpression)selector.Body).Member.Name);
        var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod("Contains", [typeof(string)])!;

        Expression? combined = null;
        foreach (var value in values)
        {
            var lower = value.ToLowerInvariant();
            var notNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(string)));
            var cond = Expression.AndAlso(
                notNull,
                Expression.Call(Expression.Call(prop, toLower), contains, Expression.Constant(lower)));
            combined = combined is null ? cond : Expression.OrElse(combined, cond);
        }

        return combined is null ? query : query.Where(Expression.Lambda<Func<Application, bool>>(combined, param));
    }

    private static IQueryable<Application> ApplyPartialMatchExcludeFilter(
        IQueryable<Application> query,
        Expression<Func<Application, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Application), "a");
        var prop = Expression.Property(param, ((MemberExpression)selector.Body).Member.Name);
        var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod("Contains", [typeof(string)])!;

        Expression? anyMatch = null;
        foreach (var value in values)
        {
            var lower = value.ToLowerInvariant();
            var notNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(string)));
            var cond = Expression.AndAlso(
                notNull,
                Expression.Call(Expression.Call(prop, toLower), contains, Expression.Constant(lower)));
            anyMatch = anyMatch is null ? cond : Expression.OrElse(anyMatch, cond);
        }

        return anyMatch is null ? query : query.Where(Expression.Lambda<Func<Application, bool>>(Expression.Not(anyMatch), param));
    }

    private static int StatusSortPriority(ApplicationStatus status) =>
        status == ApplicationStatus.ToApply ? 0 :
        status == ApplicationStatus.Interviewing ? 1 :
        status == ApplicationStatus.Applied ? 2 : 3;

    private static string EncodeCursor(int statusPriority, DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{statusPriority}|{createdAt.UtcTicks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static (int StatusPriority, DateTimeOffset CreatedAt, Guid Id)? DecodeCursor(string cursor)
    {
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "=";  break;
            }
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('|');
            if (parts.Length != 3) return null;
            var statusPriority = int.Parse(parts[0]);
            var createdAt = new DateTimeOffset(long.Parse(parts[1]), TimeSpan.Zero);
            var id = Guid.Parse(parts[2]);
            return (statusPriority, createdAt, id);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Request body for creating a new application from a job ad.</summary>
public record CreateApplicationRequest
{
    /// <summary>Gets the identifier of the job ad to apply to.</summary>
    public required Guid JobAdId { get; init; }
}

/// <summary>Request body for updating the stored job ad page content.</summary>
public record UpdateJobAdContentRequest
{
    /// <summary>Gets the new content. Null clears the stored content.</summary>
    public string? Content { get; init; }
}

/// <summary>Request body for updating the HTML content of a generated resume.</summary>
public record UpdateGeneratedResumeRequest
{
    /// <summary>Gets the updated HTML content.</summary>
    public required string HtmlContent { get; init; }
}

/// <summary>Request body for updating the HTML content of a generated application letter.</summary>
public record UpdateGeneratedLetterRequest
{
    /// <summary>Gets the updated HTML content.</summary>
    public required string HtmlContent { get; init; }
}

/// <summary>Request body for updating the job ad posting date.</summary>
public record UpdatePostedAtRequest
{
    /// <summary>Gets the new posting date, or null to clear it.</summary>
    public DateTimeOffset? PostedAt { get; init; }
}

/// <summary>Request body for updating the recorded scrape date of an application.</summary>
public record UpdateScrapedAtRequest
{
    /// <summary>Gets the new scrape date.</summary>
    public required DateTimeOffset ScrapedAt { get; init; }
}

/// <summary>Request body for transitioning an application to a new workflow status.</summary>
public record UpdateApplicationStatusRequest
{
    /// <summary>Gets the new status to transition to.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ApplicationStatus Status { get; init; }

    /// <summary>
    /// Gets the optional timestamp to record for this status transition.
    /// When null, the current UTC time is used for new history entries; existing entries keep their date.
    /// </summary>
    public DateTimeOffset? AchievedAt { get; init; }
}

/// <summary>Request body for overwriting the recorded date of an already-reached status.</summary>
public record UpdateStatusDateRequest
{
    /// <summary>Gets the status whose recorded date should be updated.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ApplicationStatus Status { get; init; }

    /// <summary>Gets the new UTC timestamp to record for this status.</summary>
    public required DateTimeOffset AchievedAt { get; init; }
}

/// <summary>Response for an interview drill plan.</summary>
public record InterviewDrillResponse(
    Guid Id,
    Guid ApplicationId,
    DateTimeOffset GeneratedAt,
    string ModelUsed,
    IList<InterviewQuestionDto> Questions);

/// <summary>A single interview question in a drill plan response.</summary>
public record InterviewQuestionDto(
    int OrderIndex,
    string Text,
    string Category,
    string? RequirementName,
    string? Answer,
    DateTimeOffset? AnsweredAt);

/// <summary>Request body for saving a drill answer.</summary>
public record SaveDrillAnswerRequest(string? Answer);

/// <summary>Response returned after a successful "Save to Drive" operation.</summary>
public record SaveToDriveResponse(string FolderId, string FolderLink);

/// <summary>Response for an interview answer evaluation.</summary>
public record InterviewAnswerEvaluationResponse(
    /// <summary>Overall quality: "good" | "satisfactory" | "needs_improvement".</summary>
    string Rating,
    /// <summary>2–3 sentence recruiter-perspective assessment.</summary>
    string Feedback,
    /// <summary>Actionable improvement tips.</summary>
    IList<string> Tips);
