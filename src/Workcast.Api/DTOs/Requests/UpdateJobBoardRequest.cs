namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for partially updating a job board. All fields are optional; only provided fields are applied.
/// </summary>
public record UpdateJobBoardRequest
{
    /// <summary>Gets the updated seed URL. Pass null to leave unchanged.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the updated human-readable name. Pass null to leave unchanged.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the updated cron schedule expression. Pass null to leave unchanged.</summary>
    public string? ScheduleCron { get; init; }

    /// <summary>Gets the desired status change. Accepted values: "active", "paused". Pass null to leave unchanged.</summary>
    public string? Status { get; init; } // "active" | "paused"
}
