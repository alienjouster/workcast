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
    private static readonly string[] AllowedModels =
    [
        "claude-haiku-4-5-20251001",
        "claude-sonnet-4-5",
        "claude-sonnet-4-6",
        "claude-opus-4-6",
    ];

    private static readonly string[] AllowedResumeTypes =
    [
        "application/pdf",
        "text/plain",
        "application/json",
    ];

    private const long MaxResumeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly ISettingsRepository _settingsRepository;

    /// <summary>
    /// Initializes a new instance of <see cref="SettingsController"/>.
    /// </summary>
    public SettingsController(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    /// <summary>Returns the current global settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        return Ok(new SettingsResponse(
            settings.AiModel,
            AllowedModels,
            settings.HasResume,
            settings.ResumeFileName,
            settings.ResumeUploadedAt));
    }

    /// <summary>Updates the AI model used for board analysis.</summary>
    [HttpPatch]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PatchAsync(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken ct)
    {
        if (!AllowedModels.Contains(request.AiModel))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "Invalid model",
                Detail = $"'{request.AiModel}' is not a recognised Anthropic model. Allowed values: {string.Join(", ", AllowedModels)}",
            });
        }

        var settings = await _settingsRepository.GetAsync(ct);
        settings.SetAiModel(request.AiModel);
        await _settingsRepository.SaveAsync(ct);

        return Ok(new SettingsResponse(
            settings.AiModel,
            AllowedModels,
            settings.HasResume,
            settings.ResumeFileName,
            settings.ResumeUploadedAt));
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

        return Ok(new SettingsResponse(
            settings.AiModel,
            AllowedModels,
            settings.HasResume,
            settings.ResumeFileName,
            settings.ResumeUploadedAt));
    }

    /// <summary>Removes the stored resume.</summary>
    [HttpDelete("resume")]
    [ProducesResponseType(typeof(SettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteResumeAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        settings.ClearResume();
        await _settingsRepository.SaveAsync(ct);

        return Ok(new SettingsResponse(
            settings.AiModel,
            AllowedModels,
            settings.HasResume,
            settings.ResumeFileName,
            settings.ResumeUploadedAt));
    }

    // ── Inline DTOs (settings has no shared domain model) ────────────────────

    /// <param name="AiModel">Active Anthropic model identifier.</param>
    /// <param name="AvailableModels">All selectable model identifiers.</param>
    /// <param name="HasResume">True when a resume file has been uploaded.</param>
    /// <param name="ResumeFileName">Original file name of the uploaded resume, or null.</param>
    /// <param name="ResumeUploadedAt">UTC timestamp of the last resume upload, or null.</param>
    public sealed record SettingsResponse(
        string AiModel,
        IEnumerable<string> AvailableModels,
        bool HasResume,
        string? ResumeFileName,
        DateTimeOffset? ResumeUploadedAt);

    /// <param name="AiModel">The model identifier to switch to.</param>
    public sealed record UpdateSettingsRequest(string AiModel);

    /// <param name="FileName">Original file name (e.g. resume.pdf).</param>
    /// <param name="ContentBase64">Base64-encoded file bytes.</param>
    /// <param name="ContentType">MIME type (application/pdf, text/plain, application/json).</param>
    public sealed record UploadResumeRequest(string FileName, string ContentBase64, string ContentType);
}
