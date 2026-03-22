using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Jobs;

/// <summary>
/// Daily Hangfire job that permanently deletes job ads that are either trashed or inactive
/// and were scraped more than 30 days ago. This enforces the trash-bin retention policy
/// displayed in the UI and keeps the database lean.
/// </summary>
public sealed class AdCleanupJob
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdCleanupJob> _logger;

    public AdCleanupJob(AppDbContext dbContext, ILogger<AdCleanupJob> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - RetentionPeriod;

        var toDelete = await _dbContext.JobAds
            .Where(a => (a.IsTrashed || !a.IsActive) && a.ScrapedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (toDelete.Count == 0) return;

        _logger.LogInformation(
            "AdCleanupJob: deleting {Count} expired job ad(s) (trashed or inactive, scraped before {Cutoff:O})",
            toDelete.Count, cutoff);

        _dbContext.JobAds.RemoveRange(toDelete);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
