namespace Workcast.Core.Models;

/// <summary>
/// A single error entry stored in the JSONB <c>errors</c> array on <see cref="Workcast.Core.Entities.ScrapeRun"/>.
/// Represents a non-fatal failure encountered while processing a specific page during a run.
/// </summary>
public record ScrapeRunError
{
    /// <summary>The URL of the page that produced the error.</summary>
    public required string Page { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string Message { get; init; }

    /// <summary>UTC timestamp when the error occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
