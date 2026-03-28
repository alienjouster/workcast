using Workcast.Core.Enums;

namespace Workcast.Core.Models;

/// <summary>
/// Records the date on which an application reached a particular status.
/// Stored as a JSONB array on the <see cref="Workcast.Core.Entities.Application"/> entity.
/// </summary>
public sealed class StatusHistoryEntry
{
    /// <summary>The status that was reached.</summary>
    public ApplicationStatus Status { get; set; }

    /// <summary>UTC timestamp when the application transitioned to this status.</summary>
    public DateTimeOffset AchievedAt { get; set; }
}
