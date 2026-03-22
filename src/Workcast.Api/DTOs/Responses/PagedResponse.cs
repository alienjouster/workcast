namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Generic cursor-paginated response wrapper.
/// </summary>
/// <typeparam name="T">The type of items in the page.</typeparam>
public record PagedResponse<T>
{
    /// <summary>Gets the items on this page.</summary>
    public required IList<T> Items { get; init; }

    /// <summary>Gets the cursor to pass as ?cursor= to retrieve the next page. Null if this is the last page.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Gets the number of items returned in this response.</summary>
    public required int Count { get; init; }

    /// <summary>Gets the total number of items matching the query across all pages.</summary>
    public int TotalCount { get; init; }
}
