using System.Text.Json.Serialization;

namespace Workcast.Core.Entities;

/// <summary>
/// Represents a single interview step in a job application process.
/// Steps are ordered by <see cref="StepNumber"/> (1-based).
/// </summary>
public sealed class InterviewStep
{
    private InterviewStep() { }

    /// <summary>Creates a new interview step for the given application.</summary>
    public static InterviewStep Create(
        Guid applicationId,
        int stepNumber,
        DateOnly? date,
        TimeOnly? time,
        int? durationMinutes,
        string timezone,
        bool isOnSite,
        string? remoteCallLink,
        List<InterviewStepInterviewer> interviewers,
        string? notes)
    {
        return new InterviewStep
        {
            ApplicationId   = applicationId,
            StepNumber      = stepNumber,
            Date            = date,
            Time            = time,
            DurationMinutes = durationMinutes,
            Timezone        = timezone,
            IsOnSite        = isOnSite,
            RemoteCallLink  = remoteCallLink,
            Interviewers    = interviewers,
            Notes           = notes,
            CreatedAt       = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>UUID primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Reference to the application this step belongs to.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>1-based sequential number (Step 1, Step 2, …).</summary>
    public int StepNumber { get; private set; }

    /// <summary>Calendar date of the interview, or null if not yet scheduled.</summary>
    public DateOnly? Date { get; private set; }

    /// <summary>Time of day for the interview, or null if not yet scheduled.</summary>
    public TimeOnly? Time { get; private set; }

    /// <summary>Expected duration of the interview in minutes, or null if not specified.</summary>
    public int? DurationMinutes { get; private set; }

    /// <summary>IANA or common timezone abbreviation (e.g. "CEST", "UTC").</summary>
    public string Timezone { get; private set; } = "CEST";

    /// <summary>True when the interview takes place on site; false when remote.</summary>
    public bool IsOnSite { get; private set; }

    /// <summary>Video or phone call link for remote interviews. Null for on-site or when not provided.</summary>
    public string? RemoteCallLink { get; private set; }

    /// <summary>People conducting the interview. Stored as a JSONB array.</summary>
    public List<InterviewStepInterviewer> Interviewers { get; private set; } = [];

    /// <summary>Free-form notes about this interview step.</summary>
    public string? Notes { get; private set; }

    /// <summary>UTC timestamp when this step was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    // ── Mutations ─────────────────────────────────────────────────────────────

    /// <summary>Updates all mutable fields of this interview step.</summary>
    public void Update(
        DateOnly? date,
        TimeOnly? time,
        int? durationMinutes,
        string timezone,
        bool isOnSite,
        string? remoteCallLink,
        List<InterviewStepInterviewer> interviewers,
        string? notes)
    {
        Date            = date;
        Time            = time;
        DurationMinutes = durationMinutes;
        Timezone        = timezone;
        IsOnSite        = isOnSite;
        RemoteCallLink  = remoteCallLink;
        Interviewers    = interviewers;
        Notes           = notes;
    }

    /// <summary>Reassigns the step number. Used during renumbering after a sibling step is deleted.</summary>
    public void Renumber(int newStepNumber) => StepNumber = newStepNumber;
}

/// <summary>A person who participates in an interview step as an interviewer.</summary>
public sealed class InterviewStepInterviewer
{
    /// <summary>Full name of the interviewer.</summary>
    public string Name { get; set; } = "";

    /// <summary>Job function or title of the interviewer (e.g. "Engineering Manager").</summary>
    public string JobFunction { get; set; } = "";
}
