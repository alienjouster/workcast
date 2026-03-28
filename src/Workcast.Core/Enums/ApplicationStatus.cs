namespace Workcast.Core.Enums;

/// <summary>
/// Represents the current stage of a job application in the user's workflow.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Job ad saved but no application submitted yet.</summary>
    ToApply = 0,

    /// <summary>Application has been submitted.</summary>
    Applied = 1,

    /// <summary>User has been invited to interview.</summary>
    Interviewing = 2,

    /// <summary>Process ended with no response from the employer.</summary>
    ClosedNoAnswer = 3,

    /// <summary>Application was rejected by the employer.</summary>
    ClosedRejected = 4,

    /// <summary>User received and accepted an offer.</summary>
    ClosedHired = 5,
}
