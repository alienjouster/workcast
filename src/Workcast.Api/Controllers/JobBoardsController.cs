using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Requests;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Scheduling;
using Workcast.Jobs;

namespace Workcast.Api.Controllers;

/// <summary>
/// Manages registered job boards — registration, configuration, scheduling, and triggering scrape runs.
/// </summary>
[ApiController]
[Route("api/job-boards")]
public sealed class JobBoardsController : ControllerBase
{
    private const string ERROR_TYPE_BASE = "https://workcast.local/errors/";

    private readonly AppDbContext _db;
    private readonly HangfireJobScheduler _scheduler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JobBoardsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="JobBoardsController"/>.
    /// </summary>
    public JobBoardsController(
        AppDbContext db,
        HangfireJobScheduler scheduler,
        IHttpClientFactory httpClientFactory,
        ILogger<JobBoardsController> logger)
    {
        _db = db;
        _scheduler = scheduler;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new job board URL and enqueues board analysis.
    /// The URL is validated as reachable before the board is persisted.
    /// Returns 202 Accepted immediately; board analysis continues asynchronously.
    /// </summary>
    /// <param name="request">The registration request containing the URL and optional name/schedule.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(JobBoardResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateJobBoardRequest request,
        CancellationToken ct)
    {
        // Validate the URL is reachable before persisting.
        var reachable = await IsUrlReachableAsync(request.Url, ct);
        if (!reachable)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}url-not-reachable",
                title: "URL Not Reachable",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: $"The URL '{request.Url}' could not be reached. Verify the URL is correct and accessible.");
        }

        var board = JobBoard.Create(request.Url, request.Name, request.ScheduleCron);
        _db.JobBoards.Add(board);
        await _db.SaveChangesAsync(ct);

        _scheduler.Enqueue<BoardAnalysisJob>(j => j.ExecuteAsync(board.Id, CancellationToken.None));

        _logger.LogInformation("Registered job board {BoardId} at {Url}, board analysis enqueued.", board.Id, board.Url);

        return AcceptedAtAction(
            nameof(GetAsync)[..^"Async".Length],
            new { id = board.Id },
            board.ToResponse(adCount: 0, includeScraperConfig: false));
    }

    /// <summary>
    /// Returns all registered job boards with their current status and ad counts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IList<JobBoardResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var boards = await _db.JobBoards
            .Select(b => new
            {
                Board = b,
                AdCount = b.JobAds.Count,
                HasActiveRun = b.ScrapeRuns.Any(r =>
                    r.Status == RunStatus.Enqueued ||
                    r.Status == RunStatus.Scheduled ||
                    r.Status == RunStatus.Awaiting  ||
                    r.Status == RunStatus.Processing),
            })
            .OrderByDescending(x => x.Board.CreatedAt)
            .ToListAsync(ct);

        var response = boards
            .Select(x => x.Board.ToResponse(x.AdCount, includeScraperConfig: false, hasActiveRun: x.HasActiveRun))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Returns a single job board by ID, including the full scraper configuration.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var result = await _db.JobBoards
            .Where(b => b.Id == id)
            .Select(b => new
            {
                Board = b,
                AdCount = b.JobAds.Count,
                HasActiveRun = b.ScrapeRuns.Any(r =>
                    r.Status == RunStatus.Enqueued ||
                    r.Status == RunStatus.Scheduled ||
                    r.Status == RunStatus.Awaiting  ||
                    r.Status == RunStatus.Processing),
            })
            .FirstOrDefaultAsync(ct);

        if (result is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        return Ok(result.Board.ToResponse(result.AdCount, includeScraperConfig: true, hasActiveRun: result.HasActiveRun));
    }

    /// <summary>
    /// Partially updates a job board. Supported fields: name, schedule_cron, status.
    /// Re-registers the Hangfire recurring job when cron or status changes.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="request">The partial update request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(JobBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateJobBoardRequest request,
        CancellationToken ct)
    {
        var result = await _db.JobBoards
            .Where(b => b.Id == id)
            .Select(b => new
            {
                Board = b,
                AdCount = b.JobAds.Count,
            })
            .FirstOrDefaultAsync(ct);

        if (result is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        var board = result.Board;
        var jobId = $"scrape-{board.Id}";
        var scheduleChanged = false;

        if (request.Url is not null)
        {
            board.UpdateUrl(request.Url);
        }

        if (request.Name is not null)
        {
            board.UpdateName(request.Name);
        }

        if (request.ScheduleCron is not null)
        {
            board.UpdateSchedule(request.ScheduleCron);
            scheduleChanged = true;
        }

        if (request.Status is not null)
        {
            switch (request.Status.ToLowerInvariant())
            {
                case "paused":
                    board.Pause();
                    _scheduler.RemoveIfExists(jobId);
                    break;

                case "active":
                    board.Resume();
                    // Only register the recurring job if the board is now Active (it may have
                    // had no ScraperConfig yet, in which case Resume() still sets Active but
                    // there is no scraper config; the job will be registered after analysis).
                    if (board.Status == Workcast.Core.Enums.BoardStatus.Active)
                    {
                        _scheduler.AddOrUpdateRecurring<ScrapeJobRunner>(
                            jobId,
                            j => j.ExecuteAsync(board.Id, TriggerSource.Scheduler, null!, CancellationToken.None),
                            board.ScheduleCron);
                    }
                    break;

                default:
                    return Problem(
                        type: $"{ERROR_TYPE_BASE}invalid-status",
                        title: "Invalid Status",
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        detail: $"Status '{request.Status}' is not valid. Accepted values: 'active', 'paused'.");
            }
        }
        else if (scheduleChanged && board.Status != Workcast.Core.Enums.BoardStatus.Paused)
        {
            // Cron changed and board is not paused — re-register with the new schedule.
            // Covers Active and Error states (error boards may recover via re-analysis).
            _scheduler.AddOrUpdateRecurring<ScrapeJobRunner>(
                jobId,
                j => j.ExecuteAsync(board.Id, TriggerSource.Scheduler, null!, CancellationToken.None),
                board.ScheduleCron);
        }

        await _db.SaveChangesAsync(ct);

        return Ok(board.ToResponse(result.AdCount, includeScraperConfig: true));
    }

    /// <summary>
    /// Deletes a job board and all associated ads and scrape runs (cascade).
    /// Also removes the Hangfire recurring job.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var board = await _db.JobBoards.FindAsync(new object[] { id }, ct);

        if (board is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        _scheduler.RemoveIfExists($"scrape-{board.Id}");
        _scheduler.DeleteBoardJobs(board.Id);

        _db.JobBoards.Remove(board);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted job board {BoardId}.", id);

        return NoContent();
    }

    /// <summary>
    /// Triggers an immediate fire-and-forget scrape run for the specified board.
    /// Returns 202 Accepted; the run executes asynchronously in Hangfire.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/refresh")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshAsync(Guid id, CancellationToken ct)
    {
        var exists = await _db.JobBoards.AnyAsync(b => b.Id == id, ct);
        if (!exists)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        _scheduler.Enqueue<ScrapeJobRunner>(j => j.ExecuteAsync(id, TriggerSource.Manual, null!, CancellationToken.None));

        _logger.LogInformation("Manual scrape refresh enqueued for board {BoardId}.", id);

        return Accepted();
    }

    /// <summary>
    /// Replaces the scraper configuration for a job board with a manually supplied one.
    /// The new config takes effect on the next scrape run. No Hangfire job is triggered.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="request">The full replacement scraper configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPut("{id:guid}/scraper-config")]
    [ProducesResponseType(typeof(JobBoardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateScraperConfigAsync(
        Guid id,
        [FromBody] UpdateScraperConfigRequest request,
        CancellationToken ct)
    {
        var board = await _db.JobBoards.FindAsync(new object[] { id }, ct);

        if (board is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        board.UpdateScraperConfig(request.ToDomain());
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Scraper config manually updated for board {BoardId}.", id);

        var adCount = await _db.JobBoards
            .Where(b => b.Id == id)
            .Select(b => b.JobAds.Count)
            .FirstAsync(ct);

        return Ok(board.ToResponse(adCount, includeScraperConfig: true));
    }

    /// <summary>
    /// Triggers a new board analysis to regenerate the scraper configuration.
    /// Returns 202 Accepted; analysis executes asynchronously in Hangfire.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/reanalyze")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReanalyzeAsync(Guid id, CancellationToken ct)
    {
        var board = await _db.JobBoards.FindAsync([id], ct);
        if (board is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        board.SetPending();
        await _db.SaveChangesAsync(ct);

        _scheduler.Enqueue<BoardAnalysisJob>(j => j.ExecuteAsync(id, CancellationToken.None));

        _logger.LogInformation("Board re-analysis enqueued for board {BoardId}.", id);

        return Accepted();
    }

    /// <summary>
    /// Returns the scrape run history for a specific job board, newest first.
    /// </summary>
    /// <param name="id">The job board identifier.</param>
    /// <param name="limit">Maximum number of runs to return. Default 50, max 200.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}/runs")]
    [ProducesResponseType(typeof(IList<ScrapeRunResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListRunsAsync(
        Guid id,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var exists = await _db.JobBoards.AnyAsync(b => b.Id == id, ct);
        if (!exists)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job board '{id}' was not found.");
        }

        limit = Math.Clamp(limit, 1, 200);

        var runs = await _db.ScrapeRuns
            .Where(r => r.JobBoardId == id)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return Ok(runs.Select(r => r.ToResponse()).ToList());
    }

    /// <summary>
    /// Checks whether the given URL is reachable via an HTTP HEAD request.
    /// A 405 Method Not Allowed response is treated as reachable (server exists but rejects HEAD).
    /// </summary>
    private async Task<bool> IsUrlReachableAsync(string url, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("UrlValidation");
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await client.SendAsync(request, ct);

            // 405 = server alive but rejects HEAD; 406 = server alive but rejects Accept headers.
            // Both mean the server is reachable.
            return response.IsSuccessStatusCode
                || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed
                || response.StatusCode == System.Net.HttpStatusCode.NotAcceptable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "URL reachability check failed for '{Url}'.", url);
            return false;
        }
    }
}
