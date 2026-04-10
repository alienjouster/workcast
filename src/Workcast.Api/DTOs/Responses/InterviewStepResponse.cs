namespace Workcast.Api.DTOs.Responses;

/// <summary>Response representation of a single interview step.</summary>
public record InterviewStepResponse
{
    /// <summary>Gets the unique identifier of the step.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the parent application identifier.</summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>Gets the 1-based sequential step number.</summary>
    public required int StepNumber { get; init; }

    /// <summary>Gets the interview date in ISO 8601 format (YYYY-MM-DD), or null.</summary>
    public string? Date { get; init; }

    /// <summary>Gets the interview time in HH:mm format, or null.</summary>
    public string? Time { get; init; }

    /// <summary>Gets the expected duration in minutes, or null.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>Gets the timezone abbreviation (e.g. "CEST").</summary>
    public required string Timezone { get; init; }

    /// <summary>Gets a value indicating whether the interview is on site (true) or remote (false).</summary>
    public required bool IsOnSite { get; init; }

    /// <summary>Gets the remote call link, or null.</summary>
    public string? RemoteCallLink { get; init; }

    /// <summary>Gets the list of interviewers.</summary>
    public required IList<InterviewStepInterviewerResponse> Interviewers { get; init; }

    /// <summary>Gets the free-form notes, or null.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the UTC timestamp when this step was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>A person conducting an interview step.</summary>
public record InterviewStepInterviewerResponse
{
    /// <summary>Gets the full name of the interviewer.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the job function or title of the interviewer.</summary>
    public required string JobFunction { get; init; }
}
