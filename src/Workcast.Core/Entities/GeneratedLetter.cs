namespace Workcast.Core.Entities;

/// <summary>
/// Stores a generated HTML application letter for a specific application.
/// Each generation creates a new row; the latest is retrieved by <c>GeneratedAt DESC</c>.
/// </summary>
public sealed class GeneratedLetter
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The application this letter was generated for.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>Navigation property to the parent application.</summary>
    public Application Application { get; private set; } = null!;

    /// <summary>The generated HTML letter content.</summary>
    public string HtmlContent { get; private set; } = string.Empty;

    /// <summary>The Anthropic model used to generate this letter.</summary>
    public string ModelUsed { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when this letter was generated.</summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>Updates the HTML content (manual edit after generation).</summary>
    public void UpdateHtmlContent(string htmlContent) => HtmlContent = htmlContent;

    // Required by EF Core.
    private GeneratedLetter() { }

    /// <summary>Creates a new generated letter record.</summary>
    public static GeneratedLetter Create(Guid applicationId, string htmlContent, string modelUsed) =>
        new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            HtmlContent = htmlContent,
            ModelUsed = modelUsed,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
}
