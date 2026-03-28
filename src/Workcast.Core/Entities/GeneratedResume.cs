namespace Workcast.Core.Entities;

/// <summary>
/// Stores a generated HTML resume for a specific application.
/// Each generation creates a new row, preserving history for future versioning.
/// </summary>
public sealed class GeneratedResume
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The application this resume was generated for.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>Navigation property to the parent application.</summary>
    public Application Application { get; private set; } = null!;

    /// <summary>The generated HTML resume content.</summary>
    public string HtmlContent { get; private set; } = string.Empty;

    /// <summary>The Anthropic model used to generate this resume.</summary>
    public string ModelUsed { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when this resume was generated.</summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>Updates the HTML content (manual edit after generation).</summary>
    public void UpdateHtmlContent(string htmlContent) => HtmlContent = htmlContent;

    // Required by EF Core.
    private GeneratedResume() { }

    /// <summary>Creates a new generated resume record.</summary>
    public static GeneratedResume Create(Guid applicationId, string htmlContent, string modelUsed) =>
        new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            HtmlContent = htmlContent,
            ModelUsed = modelUsed,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
}
