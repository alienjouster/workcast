namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for bulk job-ad actions.
/// </summary>
public sealed class BulkAdActionRequest
{
    /// <summary>The IDs of the job ads to act on.</summary>
    public required IReadOnlyList<Guid> Ids { get; init; }
}
