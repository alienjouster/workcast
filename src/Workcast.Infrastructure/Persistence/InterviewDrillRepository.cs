using Microsoft.EntityFrameworkCore;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IInterviewDrillRepository"/>.
/// Exactly one plan exists per application (unique constraint on ApplicationId).
/// Upsert is implemented as delete-then-insert to guarantee a clean replacement.
/// </summary>
internal sealed class InterviewDrillRepository : IInterviewDrillRepository
{
    private readonly AppDbContext _db;

    public InterviewDrillRepository(AppDbContext db) => _db = db;

    public async Task<InterviewDrillPlan?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default)
    {
        return await _db.InterviewDrillPlans
            .FirstOrDefaultAsync(p => p.ApplicationId == applicationId, ct)
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(InterviewDrillPlan plan, CancellationToken ct = default)
    {
        var existing = await _db.InterviewDrillPlans
            .FirstOrDefaultAsync(p => p.ApplicationId == plan.ApplicationId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.InterviewDrillPlans.Remove(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _db.InterviewDrillPlans.Add(plan);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
