using System.ComponentModel;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Scheduling;
using Workcast.Jobs;

namespace Workcast.Api.Mcp;

// ── Return types ─────────────────────────────────────────────────────────────

public record PipelineSummary(
    int TotalActiveAds,
    int UnreadAds,
    int ScoredAds,
    int UnscoredAds,
    int ScoreAbove80,
    int ScoreAbove60,
    int ScoreAbove40,
    int ScoreBelow40,
    int TotalApplications,
    Dictionary<string, int> ApplicationsByStatus,
    int ActiveBoards,
    int TotalBoards,
    bool HasResume);

public record BoardSummary(
    Guid Id,
    string? Name,
    string Url,
    string Status,
    string ScheduleCron,
    DateTimeOffset? LastScrapedAt,
    int AdCount);

public record JobAdSummary(
    Guid Id,
    string Url,
    string? Title,
    string? Company,
    string? Location,
    string? SalaryRaw,
    DateTimeOffset ScrapedAt,
    double? Score,
    bool IsRead,
    bool IsPinned,
    bool IsActive,
    bool IsTrashed);

public record RequirementSummary(
    string Name,
    string Category,
    double Score,
    bool IsOptional,
    string? Notes);

public record ScoringDetail(
    double OverallScore,
    string Summary,
    string Recommendation,
    DateTimeOffset ScoredAt,
    IList<RequirementSummary> Requirements);

public record JobAdDetail(
    Guid Id,
    string Url,
    string? Title,
    string? Company,
    string? Location,
    string? SalaryRaw,
    string? Description,
    DateTimeOffset? PostedAt,
    DateTimeOffset ScrapedAt,
    bool IsRead,
    bool IsPinned,
    bool IsActive,
    bool IsTrashed,
    ScoringDetail? Scoring);

public record ApplicationSummary(
    Guid Id,
    string? Title,
    string? Company,
    string? Location,
    string? SalaryRaw,
    string Url,
    string Status,
    DateTimeOffset CreatedAt,
    double? Score,
    bool IsTrashed);

public record StatusHistoryItem(string Status, DateTimeOffset AchievedAt);

public record ApplicationDetail(
    Guid Id,
    string? Title,
    string? Company,
    string? Location,
    string? SalaryRaw,
    string? Description,
    string Url,
    string Status,
    DateTimeOffset CreatedAt,
    double? Score,
    string? ScoringSummary,
    string? ScoringRecommendation,
    IList<RequirementSummary> ScoringRequirements,
    IList<StatusHistoryItem> StatusHistory,
    bool IsTrashed,
    bool IsScoringPending,
    bool IsResumeGenerationPending,
    bool IsLetterGenerationPending);

public record ScoringEnqueueResult(bool Enqueued, string? Error);

public record ScrapeEnqueueResult(bool Enqueued, string? Error);

// ── Tool class ───────────────────────────────────────────────────────────────

[McpServerToolType]
public sealed class WorkcastMcpTools(
    AppDbContext db,
    IAdScoringRepository scoringRepository,
    ISettingsRepository settingsRepository,
    IBackgroundJobClient backgroundJobClient,
    HangfireJobScheduler scheduler)
{
    [McpServerTool]
    [Description("Returns a statistical overview of the full recruitment pipeline: total active ads, unread count, scored count, score distribution, applications by status, board health, and whether a resume has been uploaded.")]
    public async Task<PipelineSummary> GetPipelineSummaryAsync(CancellationToken ct)
    {
        var totalActiveAds = await db.JobAds.CountAsync(a => !a.IsTrashed && a.IsActive, ct);
        var unreadAds = await db.JobAds.CountAsync(a => !a.IsTrashed && !a.IsRead, ct);

        var scores = await db.AdScorings.Select(s => s.OverallScore).ToListAsync(ct);
        var scoredAds = scores.Count;
        var unscoredAds = await db.JobAds.CountAsync(a => !a.IsTrashed, ct) - scoredAds;

        var appsByStatus = await db.Applications
            .Where(a => !a.IsTrashed)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var activeBoards = await db.JobBoards.CountAsync(b => b.Status == BoardStatus.Active, ct);
        var totalBoards = await db.JobBoards.CountAsync(ct);

        var settings = await settingsRepository.GetAsync(ct);

        return new PipelineSummary(
            TotalActiveAds: totalActiveAds,
            UnreadAds: unreadAds,
            ScoredAds: scoredAds,
            UnscoredAds: Math.Max(0, unscoredAds),
            ScoreAbove80: scores.Count(s => s >= 80),
            ScoreAbove60: scores.Count(s => s >= 60 && s < 80),
            ScoreAbove40: scores.Count(s => s >= 40 && s < 60),
            ScoreBelow40: scores.Count(s => s < 40),
            TotalApplications: appsByStatus.Sum(x => x.Count),
            ApplicationsByStatus: appsByStatus.ToDictionary(x => x.Status, x => x.Count),
            ActiveBoards: activeBoards,
            TotalBoards: totalBoards,
            HasResume: settings.HasResume);
    }

    [McpServerTool]
    [Description("Returns all registered job boards with their status, schedule, ad count, and last scrape timestamp.")]
    public async Task<IList<BoardSummary>> ListJobBoardsAsync(CancellationToken ct)
    {
        return await db.JobBoards
            .Select(b => new BoardSummary(
                b.Id,
                b.Name,
                b.Url,
                b.Status.ToString(),
                b.ScheduleCron,
                b.LastScrapedAt,
                b.JobAds.Count(a => !a.IsTrashed)))
            .ToListAsync(ct);
    }

    [McpServerTool]
    [Description("Returns a list of job ads with optional filters. Ordered by score descending (unscored ads last), then by scrape date. Returns at most 50 results. Includes trashed ads by default; use isTrashed to filter.")]
    public async Task<IList<JobAdSummary>> ListJobAdsAsync(
        [Description("Filter to ads whose title contains this string (case-sensitive).")] string? titleContains,
        [Description("Filter to ads from this company (case-sensitive partial match).")] string? company,
        [Description("Filter to ads from this job board ID.")] Guid? boardId,
        [Description("Only return ads with score >= this value (0–100).")] double? minScore,
        [Description("true = only unread ads, false = only read ads, null = no filter.")] bool? isRead,
        [Description("true = only pinned ads, false = only unpinned, null = no filter.")] bool? isPinned,
        [Description("true = only trashed ads, false = only non-trashed ads, null = all ads.")] bool? isTrashed,
        [Description("Maximum number of results to return (1–50, default 20).")] int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit == 0 ? 20 : limit, 1, 50);

        var query = db.JobAds
            .Select(a => new
            {
                Ad = a,
                Score = db.AdScorings
                    .Where(s => s.JobAdId == a.Id)
                    .Select(s => (double?)s.OverallScore)
                    .FirstOrDefault(),
            })
            .AsQueryable();

        if (titleContains is not null)
            query = query.Where(x => x.Ad.Title != null && x.Ad.Title.Contains(titleContains));
        if (company is not null)
            query = query.Where(x => x.Ad.Company != null && x.Ad.Company.Contains(company));
        if (boardId.HasValue)
            query = query.Where(x => x.Ad.JobBoardId == boardId);
        if (minScore.HasValue)
            query = query.Where(x => x.Score >= minScore);
        if (isRead.HasValue)
            query = query.Where(x => x.Ad.IsRead == isRead);
        if (isPinned.HasValue)
            query = query.Where(x => x.Ad.IsPinned == isPinned);
        if (isTrashed.HasValue)
            query = query.Where(x => x.Ad.IsTrashed == isTrashed);

        var results = await query
            .OrderByDescending(x => x.Score.HasValue)
            .ThenByDescending(x => x.Score)
            .ThenByDescending(x => x.Ad.ScrapedAt)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(x => new JobAdSummary(
            x.Ad.Id,
            x.Ad.Url,
            x.Ad.Title,
            x.Ad.Company,
            x.Ad.Location,
            x.Ad.SalaryRaw,
            x.Ad.ScrapedAt,
            x.Score,
            x.Ad.IsRead,
            x.Ad.IsPinned,
            x.Ad.IsActive,
            x.Ad.IsTrashed)).ToList();
    }

    [McpServerTool]
    [Description("Returns full details of a single job ad by ID, including the AI scoring result if one exists. Use list_job_ads first to find the ID.")]
    public async Task<JobAdDetail?> GetJobAdAsync(
        [Description("The job ad ID (UUID).")] Guid id,
        CancellationToken ct)
    {
        var ad = await db.JobAds.FindAsync(new object[] { id }, ct);
        if (ad is null) return null;

        var scoring = await scoringRepository.GetByAdIdAsync(id, ct);

        ScoringDetail? scoringDetail = scoring is null ? null : new ScoringDetail(
            scoring.OverallScore,
            scoring.Summary,
            scoring.Recommendation,
            scoring.ScoredAt,
            scoring.Requirements.Select(r => new RequirementSummary(r.Name, r.Category, r.Score, r.IsOptional, r.Notes)).ToList());

        return new JobAdDetail(
            ad.Id,
            ad.Url,
            ad.Title,
            ad.Company,
            ad.Location,
            ad.SalaryRaw,
            ad.Description,
            ad.PostedAt,
            ad.ScrapedAt,
            ad.IsRead,
            ad.IsPinned,
            ad.IsActive,
            ad.IsTrashed,
            scoringDetail);
    }

    [McpServerTool]
    [Description("Returns a list of job applications with optional filters. Includes trashed applications by default; use isTrashed to filter.")]
    public async Task<IList<ApplicationSummary>> ListApplicationsAsync(
        [Description("Filter by status: ToApply, Applied, Interviewing, ClosedNoAnswer, ClosedRejected, ClosedHired. Null returns all.")] string? status,
        [Description("Only return applications with score >= this value (0–100).")] double? minScore,
        [Description("true = only trashed applications, false = only non-trashed, null = all applications.")] bool? isTrashed,
        [Description("Maximum number of results to return (1–50, default 20).")] int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit == 0 ? 20 : limit, 1, 50);

        var query = db.Applications.AsQueryable();

        if (status is not null && Enum.TryParse<ApplicationStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(a => a.Status == parsedStatus);
        if (minScore.HasValue)
            query = query.Where(a => a.OverallScore >= minScore);
        if (isTrashed.HasValue)
            query = query.Where(a => a.IsTrashed == isTrashed);

        var results = await query
            .OrderByDescending(a => a.OverallScore.HasValue)
            .ThenByDescending(a => a.OverallScore)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return results.Select(a => new ApplicationSummary(
            a.Id,
            a.Title,
            a.Company,
            a.Location,
            a.SalaryRaw,
            a.Url,
            a.Status.ToString(),
            a.CreatedAt,
            a.OverallScore,
            a.IsTrashed)).ToList();
    }

    [McpServerTool]
    [Description("Returns full details of a single application including status history, scoring breakdown, and generation task status. Use list_applications first to find the ID.")]
    public async Task<ApplicationDetail?> GetApplicationAsync(
        [Description("The application ID (UUID).")] Guid id,
        CancellationToken ct)
    {
        var app = await db.Applications.FindAsync(new object[] { id }, ct);
        if (app is null) return null;

        return new ApplicationDetail(
            app.Id,
            app.Title,
            app.Company,
            app.Location,
            app.SalaryRaw,
            app.Description,
            app.Url,
            app.Status.ToString(),
            app.CreatedAt,
            app.OverallScore,
            app.Summary,
            app.Recommendation,
            app.Requirements.Select(r => new RequirementSummary(r.Name, r.Category, r.Score, r.IsOptional, r.Notes)).ToList(),
            app.StatusHistory.Select(h => new StatusHistoryItem(h.Status.ToString(), h.AchievedAt)).ToList(),
            app.IsTrashed,
            app.IsScoringPending,
            app.IsResumeGenerationPending,
            app.IsLetterGenerationPending);
    }

    [McpServerTool]
    [Description("Enqueues an AI scoring job for a job ad and returns immediately. Scoring typically takes 10–30 seconds. Use get_job_ad after a delay to read the result. Requires a resume to be uploaded in Workcast settings.")]
    public async Task<ScoringEnqueueResult> ScoreJobAdAsync(
        [Description("The job ad ID to score.")] Guid adId,
        CancellationToken ct)
    {
        var settings = await settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
            return new ScoringEnqueueResult(false, "No resume uploaded. Upload a resume in Workcast settings first.");

        var ad = await db.JobAds.FindAsync(new object[] { adId }, ct);
        if (ad is null)
            return new ScoringEnqueueResult(false, $"Job ad {adId} not found.");

        await scoringRepository.DeleteByAdIdAsync(adId, ct);
        ad.SetScoringPending();
        await db.SaveChangesAsync(ct);

        backgroundJobClient.Enqueue<AdScoringJob>(j => j.ExecuteAsync(adId, CancellationToken.None));
        return new ScoringEnqueueResult(true, null);
    }

    [McpServerTool]
    [Description("Triggers an immediate scrape run for a job board and returns right away. The run executes asynchronously; use list_job_ads after a moment to see new results. Only works for boards with Active status.")]
    public async Task<ScrapeEnqueueResult> TriggerScrapeAsync(
        [Description("The job board ID to scrape.")] Guid boardId,
        CancellationToken ct)
    {
        var board = await db.JobBoards.FindAsync(new object[] { boardId }, ct);
        if (board is null)
            return new ScrapeEnqueueResult(false, $"Job board {boardId} not found.");

        if (board.Status != BoardStatus.Active)
            return new ScrapeEnqueueResult(false, $"Board '{board.Name ?? board.Url}' is not active (current status: {board.Status}). Only active boards can be scraped.");

        scheduler.Enqueue<ScrapeJobRunner>(j => j.ExecuteAsync(boardId, TriggerSource.Manual, null!, CancellationToken.None));
        return new ScrapeEnqueueResult(true, null);
    }
}
