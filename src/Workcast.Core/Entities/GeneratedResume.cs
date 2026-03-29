using Workcast.Core.Models;

namespace Workcast.Core.Entities;

/// <summary>
/// Stores a generated HTML resume for a specific application.
/// Each generation or manual edit creates a new versioned row; versions are numbered sequentially
/// per application starting at 1 and never decrease (even after deletion of intermediate versions).
/// </summary>
public sealed class GeneratedResume
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The application this resume was generated for.</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>Navigation property to the parent application.</summary>
    public Application Application { get; private set; } = null!;

    /// <summary>
    /// Monotonically-increasing version number scoped to the parent application.
    /// Starts at 1; each new generation or manual save increments it.
    /// Deleted versions leave a gap — the sequence never resets.
    /// </summary>
    public int VersionNumber { get; private set; }

    /// <summary>The generated HTML resume content.</summary>
    public string HtmlContent { get; private set; } = string.Empty;

    /// <summary>The Anthropic model used to generate this resume (or inherited from the prior AI version for manual edits).</summary>
    public string ModelUsed { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when this version was created.</summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>
    /// The optimization level used when generating this version via AI.
    /// Null when <see cref="IsManualEdit"/> is true (not applicable to manual saves).
    /// </summary>
    public ResumeOptimizationLevel? OptimizationLevel { get; private set; }

    /// <summary>
    /// True when this version was created by a manual user edit (Save button in the WYSIWYG editor
    /// or the highlights toggle), false when produced by the AI generation job.
    /// </summary>
    public bool IsManualEdit { get; private set; }

    // Required by EF Core.
    private GeneratedResume() { }

    /// <summary>Creates a new versioned resume record.</summary>
    public static GeneratedResume Create(
        Guid applicationId,
        string htmlContent,
        string modelUsed,
        int versionNumber,
        ResumeOptimizationLevel? optimizationLevel,
        bool isManualEdit) =>
        new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            HtmlContent = htmlContent,
            ModelUsed = modelUsed,
            VersionNumber = versionNumber,
            GeneratedAt = DateTimeOffset.UtcNow,
            OptimizationLevel = optimizationLevel,
            IsManualEdit = isManualEdit,
        };
}
