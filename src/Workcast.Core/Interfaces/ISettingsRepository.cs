using Workcast.Core.Entities;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Provides read/write access to the global <see cref="AppSettings"/> singleton row.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Returns the single settings row, creating it with defaults if absent.</summary>
    Task<AppSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Persists any changes made to the <see cref="AppSettings"/> instance.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
