using Microsoft.EntityFrameworkCore;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAdScoringRepository"/>.
/// Scoring results are keyed by JobAdId with a unique constraint, so upsert
/// is implemented as delete-then-insert to guarantee a clean replacement.
/// </summary>
internal sealed class AdScoringRepository : IAdScoringRepository
{
    private readonly AppDbContext _db;

    public AdScoringRepository(AppDbContext db) => _db = db;

    public async Task<AdScoring?> GetByAdIdAsync(Guid jobAdId, CancellationToken ct = default)
    {
        return await _db.AdScorings
            .FirstOrDefaultAsync(s => s.JobAdId == jobAdId, ct)
            .ConfigureAwait(false);
    }

    public async Task DeleteByAdIdAsync(Guid jobAdId, CancellationToken ct = default)
    {
        var existing = await _db.AdScorings
            .FirstOrDefaultAsync(s => s.JobAdId == jobAdId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.AdScorings.Remove(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task UpsertAsync(AdScoring scoring, CancellationToken ct = default)
    {
        var existing = await _db.AdScorings
            .FirstOrDefaultAsync(s => s.JobAdId == scoring.JobAdId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.AdScorings.Remove(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _db.AdScorings.Add(scoring);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
