namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a single versioned HTML resume for an application.
/// </summary>
public record GeneratedResumeResponse
{
    /// <summary>Gets the unique identifier of this version.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the application this resume was generated for.</summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>
    /// Gets the version number. Starts at 1 and increments with each generation or manual save.
    /// Gaps may exist if versions were deleted.
    /// </summary>
    public required int VersionNumber { get; init; }

    /// <summary>Gets the generated HTML resume content.</summary>
    public required string HtmlContent { get; init; }

    /// <summary>Gets the Anthropic model used to generate or last edit this resume.</summary>
    public required string ModelUsed { get; init; }

    /// <summary>Gets the UTC timestamp when this version was created.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Gets the optimization level used during AI generation, or null for manual edits.
    /// One of: "None", "Light", "Medium", "Heavy".
    /// </summary>
    public string? OptimizationLevel { get; init; }

    /// <summary>
    /// Gets whether this version was produced by a manual user edit rather than by the AI generation job.
    /// </summary>
    public required bool IsManualEdit { get; init; }
}
