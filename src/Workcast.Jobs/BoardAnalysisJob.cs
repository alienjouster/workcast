using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Scheduling;

namespace Workcast.Jobs;

/// <summary>
/// Fire-and-forget Hangfire job that runs once when a job board is first registered.
/// Orchestrates the full board analysis pipeline: renders the board URL with Playwright,
/// sends the cleaned HTML to Claude for scraper config generation, persists the resulting
/// <c>ScraperConfig</c>, activates the board, registers its recurring scrape schedule,
/// and triggers an immediate first scrape run.
/// See TECHSPEC section 5.1 for the complete board registration flow.
/// </summary>
public sealed class BoardAnalysisJob
{
    private readonly AppDbContext _dbContext;
    private readonly IJobBoardAnalyzer _boardAnalyzer;
    private readonly HangfireJobScheduler _jobScheduler;
    private readonly ILogger<BoardAnalysisJob> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="BoardAnalysisJob"/>.
    /// </summary>
    /// <param name="dbContext">EF Core database context.</param>
    /// <param name="boardAnalyzer">Orchestrates Playwright rendering and Claude board analysis.</param>
    /// <param name="jobScheduler">Wrapper around Hangfire's static scheduling API.</param>
    /// <param name="logger">Logger for this job.</param>
    public BoardAnalysisJob(
        AppDbContext dbContext,
        IJobBoardAnalyzer boardAnalyzer,
        HangfireJobScheduler jobScheduler,
        ILogger<BoardAnalysisJob> logger)
    {
        _dbContext = dbContext;
        _boardAnalyzer = boardAnalyzer;
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    /// <summary>
    /// Executes the board analysis pipeline for the specified job board.
    /// On success: activates the board, registers its recurring scrape job, and enqueues
    /// an immediate first scrape run. On failure: sets the board status to
    /// <see cref="BoardStatus.Error"/> and re-throws so Hangfire applies its retry policy.
    /// </summary>
    /// <param name="jobBoardId">The ID of the job board to analyse.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync(Guid jobBoardId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting board analysis for board {BoardId}", jobBoardId);

        var board = await _dbContext.JobBoards
            .FirstOrDefaultAsync(b => b.Id == jobBoardId, ct)
            .ConfigureAwait(false);

        if (board is null)
        {
            _logger.LogWarning("Board {BoardId} not found — analysis aborted", jobBoardId);
            return;
        }

        try
        {
            var result = await _boardAnalyzer.AnalyzeAsync(board.Url, ct).ConfigureAwait(false);
            var config = result.ToScraperConfig();

            board.Activate(config);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Board {BoardId} analysed successfully. Confidence: {Score:F2}. " +
                "Pagination: {Pagination}. Selector: {Selector}",
                jobBoardId,
                result.ConfidenceScore,
                result.PaginationType,
                result.JobCardSelector);

            // Register the recurring scrape job using the board's cron schedule.
            // Using the stable ID "scrape-{boardId}" means a re-register on schedule
            // change (PATCH endpoint) will update rather than duplicate the job.
            _jobScheduler.AddOrUpdateRecurring<ScrapeJobRunner>(
                $"scrape-{board.Id}",
                x => x.ExecuteAsync(board.Id, TriggerSource.Scheduler, CancellationToken.None),
                board.ScheduleCron);

            // Trigger an immediate first scrape so the board is populated without
            // waiting for the first scheduled window.
            _jobScheduler.Enqueue<ScrapeJobRunner>(
                x => x.ExecuteAsync(board.Id, TriggerSource.Scheduler, CancellationToken.None));

            _logger.LogInformation(
                "Recurring scrape job registered and immediate first run enqueued for board {BoardId}",
                jobBoardId);
        }
        catch (OperationCanceledException)
        {
            // Let cancellation propagate without modifying board status.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Board analysis failed for board {BoardId}", jobBoardId);

            board.SetError();
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            // Re-throw so Hangfire records the failure and applies the retry policy
            // (1 attempts with exponential backoff, per TECHSPEC section 11.2).
            throw;
        }
    }
}
