namespace Workcast.Core.Enums;

/// <summary>
/// Lifecycle status of a registered job board.
/// </summary>
public enum BoardStatus
{
    /// <summary>Board has been registered but board analysis has not yet completed.</summary>
    Pending,

    /// <summary>Board analysis succeeded and recurring scraping is active.</summary>
    Active,

    /// <summary>Scraping is paused by the user.</summary>
    Paused,

    /// <summary>Board analysis or repeated scraping has failed.</summary>
    Error,
}
