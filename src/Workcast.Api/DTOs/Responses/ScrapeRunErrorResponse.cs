namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a single error that occurred during a scrape run.
/// </summary>
public record ScrapeRunErrorResponse
{
    /// <summary>Gets the URL of the page that produced the error.</summary>
    public required string Page { get; init; }

    /// <summary>Gets the error message describing what went wrong.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the UTC timestamp when the error occurred.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
