using Microsoft.EntityFrameworkCore;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ISettingsRepository"/>.
/// The settings row (Id = 1) is seeded by <c>AppSettingsConfiguration</c> so
/// <see cref="GetAsync"/> will always find a row after migrations have run.
/// </summary>
internal sealed class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;

    public SettingsRepository(AppDbContext db) => _db = db;

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        return await _db.AppSettings.SingleAsync(ct).ConfigureAwait(false);
    }

    public Task SaveAsync(CancellationToken ct = default)
    {
        return _db.SaveChangesAsync(ct);
    }
}
