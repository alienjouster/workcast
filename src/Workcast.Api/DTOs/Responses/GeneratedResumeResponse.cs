namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a generated HTML resume for an application.
/// </summary>
public record GeneratedResumeResponse
{
    /// <summary>Gets the unique identifier of this generation.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the application this resume was generated for.</summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>Gets the generated HTML resume content.</summary>
    public required string HtmlContent { get; init; }

    /// <summary>Gets the Anthropic model used to generate this resume.</summary>
    public required string ModelUsed { get; init; }

    /// <summary>Gets the UTC timestamp when this resume was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
