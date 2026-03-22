namespace Workcast.Core.Entities;

/// <summary>
/// Global application settings stored in the database. Uses a singleton-row pattern:
/// exactly one row exists with <see cref="Id"/> = 1. Seeded on first migration.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Always 1 — only one settings row exists.</summary>
    public int Id { get; private set; }

    /// <summary>
    /// Anthropic model identifier used for board analysis AI calls.
    /// Overrides the value in <c>appsettings.json</c> at runtime.
    /// </summary>
    public string AiModel { get; private set; } = "claude-sonnet-4-5";

    /// <summary>Original file name of the uploaded resume.</summary>
    public string? ResumeFileName { get; private set; }

    /// <summary>Raw bytes of the uploaded resume file.</summary>
    public byte[]? ResumeContent { get; private set; }

    /// <summary>MIME type of the uploaded resume (e.g. "application/pdf", "text/plain").</summary>
    public string? ResumeContentType { get; private set; }

    /// <summary>UTC timestamp when the resume was last uploaded.</summary>
    public DateTimeOffset? ResumeUploadedAt { get; private set; }

    /// <summary>True when a resume file has been uploaded.</summary>
    public bool HasResume => ResumeContent is not null;

    // Required by EF Core.
    private AppSettings() { }

    /// <summary>Creates the singleton settings row with default values.</summary>
    public static AppSettings CreateDefault() => new() { Id = 1 };

    /// <summary>Updates the AI model identifier.</summary>
    public void SetAiModel(string model) => AiModel = model;

    /// <summary>Stores a new resume, replacing any previously uploaded file.</summary>
    public void SetResume(string fileName, byte[] content, string contentType)
    {
        ResumeFileName = fileName;
        ResumeContent = content;
        ResumeContentType = contentType;
        ResumeUploadedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Removes the stored resume.</summary>
    public void ClearResume()
    {
        ResumeFileName = null;
        ResumeContent = null;
        ResumeContentType = null;
        ResumeUploadedAt = null;
    }
}
