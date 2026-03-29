namespace Workcast.Core.Entities;

/// <summary>
/// Global application settings stored in the database. Uses a singleton-row pattern:
/// exactly one row exists with <see cref="Id"/> = 1. Seeded on first migration.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Always 1 — only one settings row exists.</summary>
    public int Id { get; private set; }

    /// <summary>Anthropic model identifier used for board analysis AI calls.</summary>
    public string BoardAnalyzerModel { get; private set; } = "claude-sonnet-4-5";

    /// <summary>Anthropic model identifier used for job ad scoring AI calls.</summary>
    public string ScoringModel { get; private set; } = "claude-haiku-4-5-20251001";

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

    /// <summary>Original file name of the uploaded resume template.</summary>
    public string? ResumeTemplateFileName { get; private set; }

    /// <summary>HTML content of the resume template (defines visual structure and styling).</summary>
    public string? ResumeTemplateContent { get; private set; }

    /// <summary>UTC timestamp when the resume template was last uploaded.</summary>
    public DateTimeOffset? ResumeTemplateUploadedAt { get; private set; }

    /// <summary>True when a resume template has been uploaded.</summary>
    public bool HasResumeTemplate => ResumeTemplateContent is not null;

    /// <summary>Anthropic model identifier used for custom resume generation.</summary>
    public string ResumeGenerationModel { get; private set; } = "claude-sonnet-4-6";

    /// <summary>Anthropic model identifier used for application letter generation.</summary>
    public string LetterGenerationModel { get; private set; } = "claude-sonnet-4-6";

    // Required by EF Core.
    private AppSettings() { }

    /// <summary>Creates the singleton settings row with default values.</summary>
    public static AppSettings CreateDefault() => new() { Id = 1 };

    /// <summary>Updates the board analyzer model identifier.</summary>
    public void SetBoardAnalyzerModel(string model) => BoardAnalyzerModel = model;

    /// <summary>Updates the scoring model identifier.</summary>
    public void SetScoringModel(string model) => ScoringModel = model;

    /// <summary>Updates the resume generation model identifier.</summary>
    public void SetResumeGenerationModel(string model) => ResumeGenerationModel = model;

    /// <summary>Updates the letter generation model identifier.</summary>
    public void SetLetterGenerationModel(string model) => LetterGenerationModel = model;

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

    /// <summary>Stores a new resume template (HTML), replacing any previously uploaded template.</summary>
    public void SetResumeTemplate(string fileName, string htmlContent)
    {
        ResumeTemplateFileName = fileName;
        ResumeTemplateContent = htmlContent;
        ResumeTemplateUploadedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Removes the stored resume template.</summary>
    public void ClearResumeTemplate()
    {
        ResumeTemplateFileName = null;
        ResumeTemplateContent = null;
        ResumeTemplateUploadedAt = null;
    }
}
