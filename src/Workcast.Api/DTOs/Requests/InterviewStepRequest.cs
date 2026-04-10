namespace Workcast.Api.DTOs.Requests;

/// <summary>Request body for creating a new interview step.</summary>
public record CreateInterviewStepRequest
{
    /// <summary>Gets the interview date in ISO 8601 format (YYYY-MM-DD), or null.</summary>
    public string? Date { get; init; }

    /// <summary>Gets the interview time in HH:mm format, or null.</summary>
    public string? Time { get; init; }

    /// <summary>Gets the expected duration in minutes, or null.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>Gets the timezone abbreviation. Defaults to "CEST" when omitted.</summary>
    public string Timezone { get; init; } = "CEST";

    /// <summary>Gets a value indicating whether the interview is on site (true) or remote (false).</summary>
    public bool IsOnSite { get; init; }

    /// <summary>Gets the remote call link, or null.</summary>
    public string? RemoteCallLink { get; init; }

    /// <summary>Gets the list of interviewers.</summary>
    public IList<InterviewerRequest> Interviewers { get; init; } = [];

    /// <summary>Gets the free-form notes, or null.</summary>
    public string? Notes { get; init; }
}

/// <summary>Request body for updating an existing interview step.</summary>
public record UpdateInterviewStepRequest
{
    /// <summary>Gets the interview date in ISO 8601 format (YYYY-MM-DD), or null.</summary>
    public string? Date { get; init; }

    /// <summary>Gets the interview time in HH:mm format, or null.</summary>
    public string? Time { get; init; }

    /// <summary>Gets the expected duration in minutes, or null.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>Gets the timezone abbreviation.</summary>
    public required string Timezone { get; init; }

    /// <summary>Gets a value indicating whether the interview is on site (true) or remote (false).</summary>
    public required bool IsOnSite { get; init; }

    /// <summary>Gets the remote call link, or null.</summary>
    public string? RemoteCallLink { get; init; }

    /// <summary>Gets the list of interviewers.</summary>
    public required IList<InterviewerRequest> Interviewers { get; init; }

    /// <summary>Gets the free-form notes, or null.</summary>
    public string? Notes { get; init; }
}

/// <summary>An interviewer within a step request.</summary>
public record InterviewerRequest
{
    /// <summary>Gets the full name of the interviewer.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the job function or title of the interviewer.</summary>
    public required string JobFunction { get; init; }
}
