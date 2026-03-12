namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for registering a new job board.
/// </summary>
public record CreateJobBoardRequest
{
    /// <summary>Gets the seed URL of the job board to register.</summary>
    public required string Url { get; init; }

    /// <summary>Gets an optional human-readable name for the board.</summary>
    public string? Name { get; init; }

    /// <summary>Gets an optional cron expression for the scrape schedule. Defaults to hourly if not provided.</summary>
    public string? ScheduleCron { get; init; }
}
