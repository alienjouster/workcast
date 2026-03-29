namespace Workcast.Api.DTOs.Responses;

/// <summary>
/// Response representation of a generated HTML application letter for an application.
/// </summary>
public record GeneratedLetterResponse
{
    /// <summary>Gets the unique identifier of this generation.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the application this letter was generated for.</summary>
    public required Guid ApplicationId { get; init; }

    /// <summary>Gets the generated HTML letter content.</summary>
    public required string HtmlContent { get; init; }

    /// <summary>Gets the Anthropic model used to generate this letter.</summary>
    public required string ModelUsed { get; init; }

    /// <summary>Gets the UTC timestamp when this letter was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
