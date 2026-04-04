using Workcast.Core.Entities;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Provides persistence operations for <see cref="InterviewDrillPlan"/> entities.
/// Exactly one plan exists per application — upsert replaces the previous plan.
/// </summary>
public interface IInterviewDrillRepository
{
    /// <summary>Returns the drill plan for the given application, or null if none exists.</summary>
    Task<InterviewDrillPlan?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the existing plan for the application (if any) with <paramref name="plan"/>.
    /// Implemented as delete-then-insert to guarantee a clean replacement.
    /// </summary>
    Task UpsertAsync(InterviewDrillPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Saves the user's answer for a single question (identified by <paramref name="orderIndex"/>)
    /// on the plan belonging to <paramref name="applicationId"/>.
    /// Returns false if no plan exists for the application.
    /// </summary>
    Task<bool> SaveAnswerAsync(Guid applicationId, int orderIndex, string? answer, CancellationToken ct = default);
}
