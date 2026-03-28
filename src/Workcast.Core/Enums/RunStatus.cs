namespace Workcast.Core.Enums;

/// <summary>
/// Execution status of a single scrape run. Maps 1-to-1 with Hangfire job states
/// so the UI reflects the true position of the job in the Hangfire pipeline.
/// </summary>
public enum RunStatus
{
    /// <summary>Job is waiting in the Hangfire queue for a free worker.</summary>
    Enqueued,

    /// <summary>Job is waiting for its scheduled time (e.g. retry back-off delay).</summary>
    Scheduled,

    /// <summary>Job is waiting for a parent job continuation to complete.</summary>
    Awaiting,

    /// <summary>Job is actively executing on a Hangfire worker.</summary>
    Processing,

    /// <summary>Run finished without errors.</summary>
    Completed,

    /// <summary>Run aborted due to a fatal error.</summary>
    Failed,

    /// <summary>Run completed but some pages or ads produced errors.</summary>
    Partial,

    /// <summary>Job was manually deleted from the Hangfire dashboard.</summary>
    Deleted,
}
