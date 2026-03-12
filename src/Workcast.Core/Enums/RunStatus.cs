namespace Workcast.Core.Enums;

/// <summary>
/// Execution status of a single scrape run.
/// </summary>
public enum RunStatus
{
    /// <summary>Run is currently in progress.</summary>
    Running,

    /// <summary>Run finished without errors.</summary>
    Completed,

    /// <summary>Run aborted due to a fatal error.</summary>
    Failed,

    /// <summary>Run completed but some pages or ads produced errors.</summary>
    Partial,
}
