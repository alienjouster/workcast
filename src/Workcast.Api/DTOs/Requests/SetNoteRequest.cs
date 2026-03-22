namespace Workcast.Api.DTOs.Requests;

/// <summary>Request body for setting or clearing a personal note on a job ad.</summary>
public sealed class SetNoteRequest
{
    /// <summary>The note text. Null or whitespace-only clears the note.</summary>
    public string? Note { get; init; }
}
