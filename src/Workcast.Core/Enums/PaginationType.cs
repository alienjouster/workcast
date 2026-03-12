namespace Workcast.Core.Enums;

/// <summary>
/// Pagination strategy identified by the AI board analysis.
/// </summary>
public enum PaginationType
{
    /// <summary>Pagination is driven by a URL query parameter (e.g. ?page=2).</summary>
    UrlParam,

    /// <summary>Pagination is driven by clicking a "next" button on the page.</summary>
    NextButton,

    /// <summary>Pagination is driven by infinite scroll — new items load as the page scrolls.</summary>
    InfiniteScroll,

    /// <summary>All job listings appear on a single page with no pagination.</summary>
    None,
}
