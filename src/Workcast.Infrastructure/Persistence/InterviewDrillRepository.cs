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

    public async Task<bool> SaveAnswerAsync(Guid applicationId, int orderIndex, string? answer, CancellationToken ct = default)
    {
        var plan = await _db.InterviewDrillPlans
            .FirstOrDefaultAsync(p => p.ApplicationId == applicationId, ct)
            .ConfigureAwait(false);

        if (plan is null) return false;

        var question = plan.Questions.FirstOrDefault(q => q.OrderIndex == orderIndex);
        if (question is null) return false;

        question.Answer     = answer;
        question.AnsweredAt = string.IsNullOrWhiteSpace(answer) ? null : DateTimeOffset.UtcNow;

        // The Questions column is a JSONB blob tracked via value conversion — mark it modified explicitly.
        _db.Entry(plan).Property(p => p.Questions).IsModified = true;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ClearAnswersAsync(Guid applicationId, CancellationToken ct = default)
    {
        var plan = await _db.InterviewDrillPlans
            .FirstOrDefaultAsync(p => p.ApplicationId == applicationId, ct)
            .ConfigureAwait(false);

        if (plan is null) return false;

        foreach (var question in plan.Questions)
        {
            question.Answer     = null;
            question.AnsweredAt = null;
        }

        _db.Entry(plan).Property(p => p.Questions).IsModified = true;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
