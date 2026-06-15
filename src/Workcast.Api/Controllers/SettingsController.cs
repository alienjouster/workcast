using Microsoft.AspNetCore.Mvc;
using Workcast.Core.Interfaces;


namespace Workcast.Api.Controllers;

/// <summary>
/// Exposes global application settings — currently the AI model used for board analysis.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SettingsController : ControllerBase
{
    private static readonly string[] AllowedResumeTypes =
    [
        "application/pdf",
        "text/plain",
        "application/json",
    ];

    private const long MaxResumeBytes = 5 * 1024 * 1024; // 5 MB
    private const long MaxTemplateBytes = 2 * 1024 * 1024; // 2 MB

    private readonly ISettingsRepository _settingsRepository;
    private readonly IAnthropicModelsService _modelsService;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsController"/>.
    /// </summary>
    public SettingsController(ISettingsRepository settingsRepository, IAnthropicModelsService modelsService)
    {
        _settingsRepository = settingsRepository;
        _modelsService = modelsService;
    }

    /// <summary>Returns the current global settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        var models = await _modelsService.GetModelsAsync(ct);
        return Ok(ToResponse(settings, models));
    }

    /// <summary>Updates the AI models used for board analysis, scoring, and/or resume generation.</summary>
    [HttpPatch]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PatchAsync(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken ct)
    {
        static bool IsValidModel(string id) =>
            !string.IsNullOrWhiteSpace(id) &&
            id.StartsWith("claude-", StringComparison.OrdinalIgnoreCase);

        if (!IsValidModel(request.BoardAnalyzerModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid board analyzer model",
                Detail = $"'{request.BoardAnalyzerModel}' is not a recognised Anthropic model." });

        if (!IsValidModel(request.ScoringModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid scoring model",
                Detail = $"'{request.ScoringModel}' is not a recognised Anthropic model." });

        if (!IsValidModel(request.ResumeGenerationModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid resume generation model",
                Detail = $"'{request.ResumeGenerationModel}' is not a recognised Anthropic model." });

        if (!IsValidModel(request.LetterGenerationModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid letter generation model",
                Detail = $"'{request.LetterGenerationModel}' is not a recognised Anthropic model." });

        if (!IsValidModel(request.InterviewTrainerModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid interview trainer model",
                Detail = $"'{request.InterviewTrainerModel}' is not a recognised Anthropic model." });

        if (!IsValidModel(request.InterviewAnswerEvaluationModel))
            return UnprocessableEntity(new ProblemDetails { Title = "Invalid interview answer evaluation model",
                Detail = $"'{request.InterviewAnswerEvaluationModel}' is not a recognised Anthropic model." });

        var models = await _modelsService.GetModelsAsync(ct);

        if (request.BoardAnalyzerMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid board analyzer max tokens",
                Detail = "BoardAnalyzerMaxTokens must be a positive integer.",
            });
        }

        if (request.ScoringMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid scoring max tokens",
                Detail = "ScoringMaxTokens must be a positive integer.",
            });
        }

        if (request.ResumeGenerationMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid resume generation max tokens",
                Detail = "ResumeGenerationMaxTokens must be a positive integer.",
            });
        }

        if (request.LetterGenerationMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid letter generation max tokens",
                Detail = "LetterGenerationMaxTokens must be a positive integer.",
            });
        }

        if (request.InterviewTrainerMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid interview trainer max tokens",
                Detail = "InterviewTrainerMaxTokens must be a positive integer.",
            });
        }

        if (request.InterviewAnswerEvaluationMaxTokens <= 0)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid interview answer evaluation max tokens",
                Detail = "InterviewAnswerEvaluationMaxTokens must be a positive integer.",
            });
        }

        var settings = await _settingsRepository.GetAsync(ct);
        settings.SetBoardAnalyzerModel(request.BoardAnalyzerModel);
        settings.SetScoringModel(request.ScoringModel);
        settings.SetResumeGenerationModel(request.ResumeGenerationModel);
        settings.SetLetterGenerationModel(request.LetterGenerationModel);
        settings.SetInterviewTrainerModel(request.InterviewTrainerModel);
        settings.SetInterviewAnswerEvaluationModel(request.InterviewAnswerEvaluationModel);
        settings.SetBoardAnalyzerMaxTokens(request.BoardAnalyzerMaxTokens);
        settings.SetScoringMaxTokens(request.ScoringMaxTokens);
        settings.SetResumeGenerationMaxTokens(request.ResumeGenerationMaxTokens);
        settings.SetLetterGenerationMaxTokens(request.LetterGenerationMaxTokens);
        settings.SetInterviewTrainerMaxTokens(request.InterviewTrainerMaxTokens);
        settings.SetInterviewAnswerEvaluationMaxTokens(request.InterviewAnswerEvaluationMaxTokens);
        await _settingsRepository.SaveAsync(ct);

        return Ok(ToResponse(settings, models));
    }

    /// <summary>
    /// Uploads or replaces the user's resume. Accepted formats: PDF, TXT, JSON (max 5 MB).
    /// File content must be base64-encoded in the request body.
    /// </summary>
    [HttpPut("resume")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadResumeAsync(
        [FromBody] UploadResumeRequest request,
        CancellationToken ct)
    {
        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid base64",
                Detail = "ContentBase64 is not valid base64.",
            });
        }

        if (content.Length > MaxResumeBytes)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "File too large",
                Detail = $"Resume must be 5 MB or smaller. Received {content.Length / 1024} KB.",
            });
        }

        var contentType = request.ContentType.ToLowerInvariant();
        if (!AllowedResumeTypes.Contains(contentType))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Unsupported file type",
                Detail = $"Only PDF, plain text, and JSON resumes are accepted. Received: '{request.ContentType}'.",
            });
        }

        var settings = await _settingsRepository.GetAsync(ct);
        settings.SetResume(request.FileName, content, contentType);
        await _settingsRepository.SaveAsync(ct);

        var models = await _modelsService.GetModelsAsync(ct);
        return Ok(ToResponse(settings, models));
    }

    /// <summary>Removes the stored resume.</summary>
    [HttpDelete("resume")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteResumeAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        settings.ClearResume();
        await _settingsRepository.SaveAsync(ct);

        var models = await _modelsService.GetModelsAsync(ct);
        return Ok(ToResponse(settings, models));
    }

    /// <summary>Returns the stored resume as a UTF-8 string. 404 if no resume is stored.</summary>
    [HttpGet("resume/content")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResumeContentAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
            return NotFound();

        return Ok(System.Text.Encoding.UTF8.GetString(settings.ResumeContent!));
    }

    /// <summary>
    /// Uploads or replaces the resume HTML template. Accepted format: HTML text (max 2 MB).
    /// File content must be base64-encoded in the request body.
    /// </summary>
    [HttpPut("resume-template")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadResumeTemplateAsync(
        [FromBody] UploadResumeTemplateRequest request,
        CancellationToken ct)
    {
        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(request.ContentBase64);
        }
        catch (FormatException)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid base64",
                Detail = "ContentBase64 is not valid base64.",
            });
        }

        if (contentBytes.Length > MaxTemplateBytes)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "File too large",
                Detail = $"Template must be 2 MB or smaller. Received {contentBytes.Length / 1024} KB.",
            });
        }

        var htmlContent = System.Text.Encoding.UTF8.GetString(contentBytes);

        var settings = await _settingsRepository.GetAsync(ct);
        settings.SetResumeTemplate(request.FileName, htmlContent);
        await _settingsRepository.SaveAsync(ct);

        var models = await _modelsService.GetModelsAsync(ct);
        return Ok(ToResponse(settings, models));
    }

    /// <summary>Removes the stored resume template.</summary>
    [HttpDelete("resume-template")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteResumeTemplateAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        settings.ClearResumeTemplate();
        await _settingsRepository.SaveAsync(ct);

        var models = await _modelsService.GetModelsAsync(ct);
        return Ok(ToResponse(settings, models));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SettingsResponse ToResponse(Core.Entities.AppSettings s, IReadOnlyList<AvailableModelDto> models) => new(
        s.BoardAnalyzerModel,
        s.ScoringModel,
        s.ResumeGenerationModel,
        s.LetterGenerationModel,
        s.InterviewTrainerModel,
        s.InterviewAnswerEvaluationModel,
        s.BoardAnalyzerMaxTokens,
        s.ScoringMaxTokens,
        s.ResumeGenerationMaxTokens,
        s.LetterGenerationMaxTokens,
        s.InterviewTrainerMaxTokens,
        s.InterviewAnswerEvaluationMaxTokens,
        models,
        s.HasResume,
        s.ResumeFileName,
        s.ResumeUploadedAt,
        s.HasResumeTemplate,
        s.ResumeTemplateFileName,
        s.ResumeTemplateUploadedAt,
        s.IsGoogleDriveConnected,
        s.GoogleDriveBasePath);

    // ── Inline DTOs (settings has no shared domain model) ────────────────────

    /// <param name="BoardAnalyzerModel">Active Anthropic model for board analysis.</param>
    /// <param name="ScoringModel">Active Anthropic model for job ad scoring.</param>
    /// <param name="ResumeGenerationModel">Active Anthropic model for custom resume generation.</param>
    /// <param name="LetterGenerationModel">Active Anthropic model for application letter generation.</param>
    /// <param name="InterviewTrainerModel">Active Anthropic model for interview drill generation.</param>
    /// <param name="InterviewAnswerEvaluationModel">Active Anthropic model for interview answer evaluation.</param>
    /// <param name="BoardAnalyzerMaxTokens">Max tokens for board analysis AI calls.</param>
    /// <param name="ScoringMaxTokens">Max tokens for job ad scoring AI calls.</param>
    /// <param name="ResumeGenerationMaxTokens">Max tokens for resume generation AI calls.</param>
    /// <param name="LetterGenerationMaxTokens">Max tokens for letter generation AI calls.</param>
    /// <param name="InterviewTrainerMaxTokens">Max tokens for interview drill generation AI calls.</param>
    /// <param name="InterviewAnswerEvaluationMaxTokens">Max tokens for interview answer evaluation AI calls.</param>
    /// <param name="AvailableModels">All selectable models with their display names.</param>
    /// <param name="HasResume">True when a resume file has been uploaded.</param>
    /// <param name="ResumeFileName">Original file name of the uploaded resume, or null.</param>
    /// <param name="ResumeUploadedAt">UTC timestamp of the last resume upload, or null.</param>
    /// <param name="HasResumeTemplate">True when an HTML resume template has been uploaded.</param>
    /// <param name="ResumeTemplateFileName">Original file name of the uploaded template, or null.</param>
    /// <param name="ResumeTemplateUploadedAt">UTC timestamp of the last template upload, or null.</param>
    /// <param name="IsGoogleDriveConnected">True when a Google Drive refresh token has been stored.</param>
    /// <param name="GoogleDriveBasePath">Base folder path used for new application saves.</param>
    public sealed record SettingsResponse(
        string BoardAnalyzerModel,
        string ScoringModel,
        string ResumeGenerationModel,
        string LetterGenerationModel,
        string InterviewTrainerModel,
        string InterviewAnswerEvaluationModel,
        int BoardAnalyzerMaxTokens,
        int ScoringMaxTokens,
        int ResumeGenerationMaxTokens,
        int LetterGenerationMaxTokens,
        int InterviewTrainerMaxTokens,
        int InterviewAnswerEvaluationMaxTokens,
        IEnumerable<AvailableModelDto> AvailableModels,
        bool HasResume,
        string? ResumeFileName,
        DateTimeOffset? ResumeUploadedAt,
        bool HasResumeTemplate,
        string? ResumeTemplateFileName,
        DateTimeOffset? ResumeTemplateUploadedAt,
        bool IsGoogleDriveConnected,
        string GoogleDriveBasePath);

    /// <param name="BoardAnalyzerModel">Model identifier for board analysis.</param>
    /// <param name="ScoringModel">Model identifier for job ad scoring.</param>
    /// <param name="ResumeGenerationModel">Model identifier for custom resume generation.</param>
    /// <param name="LetterGenerationModel">Model identifier for application letter generation.</param>
    /// <param name="InterviewTrainerModel">Model identifier for interview drill generation.</param>
    /// <param name="InterviewAnswerEvaluationModel">Model identifier for interview answer evaluation.</param>
    /// <param name="BoardAnalyzerMaxTokens">Max tokens for board analysis AI calls.</param>
    /// <param name="ScoringMaxTokens">Max tokens for job ad scoring AI calls.</param>
    /// <param name="ResumeGenerationMaxTokens">Max tokens for resume generation AI calls.</param>
    /// <param name="LetterGenerationMaxTokens">Max tokens for letter generation AI calls.</param>
    /// <param name="InterviewTrainerMaxTokens">Max tokens for interview drill generation AI calls.</param>
    /// <param name="InterviewAnswerEvaluationMaxTokens">Max tokens for interview answer evaluation AI calls.</param>
    public sealed record UpdateSettingsRequest(
        string BoardAnalyzerModel,
        string ScoringModel,
        string ResumeGenerationModel,
        string LetterGenerationModel,
        string InterviewTrainerModel,
        string InterviewAnswerEvaluationModel,
        int BoardAnalyzerMaxTokens,
        int ScoringMaxTokens,
        int ResumeGenerationMaxTokens,
        int LetterGenerationMaxTokens,
        int InterviewTrainerMaxTokens,
        int InterviewAnswerEvaluationMaxTokens);

    /// <param name="FileName">Original file name (e.g. resume.pdf).</param>
    /// <param name="ContentBase64">Base64-encoded file bytes.</param>
    /// <param name="ContentType">MIME type (application/pdf, text/plain, application/json).</param>
    public sealed record UploadResumeRequest(string FileName, string ContentBase64, string ContentType);

    /// <param name="FileName">Original file name (e.g. resume-template.html).</param>
    /// <param name="ContentBase64">Base64-encoded HTML file bytes.</param>
    public sealed record UploadResumeTemplateRequest(string FileName, string ContentBase64);
}
