namespace Workcast.Core.Enums;

/// <summary>
/// Describes what triggered a scrape run.
/// </summary>
public enum TriggerSource
{
    /// <summary>Run was triggered automatically by the Hangfire recurring schedule.</summary>
    Scheduler,

    /// <summary>Run was triggered manually via the API refresh endpoint.</summary>
    Manual,
}
