using Workcast.Core.Entities;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Provides read and write access to <see cref="AdScoring"/> records.
/// Only one scoring result exists per job ad at any time.
/// </summary>
public interface IAdScoringRepository
{
    /// <summary>Returns the scoring result for the given job ad, or null if none exists.</summary>
    Task<AdScoring?> GetByAdIdAsync(Guid jobAdId, CancellationToken ct = default);

    /// <summary>
    /// Saves a scoring result, replacing any previously existing record for the same job ad.
    /// </summary>
    Task UpsertAsync(AdScoring scoring, CancellationToken ct = default);

    /// <summary>Deletes the scoring result for the given job ad if one exists.</summary>
    Task DeleteByAdIdAsync(Guid jobAdId, CancellationToken ct = default);
}
