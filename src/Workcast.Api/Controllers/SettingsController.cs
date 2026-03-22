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
        return Ok(new SettingsResponse(settings.AiModel, AllowedModels));
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

        return Ok(new SettingsResponse(settings.AiModel, AllowedModels));
    }

    // ── Inline DTOs (settings has no shared domain model) ────────────────────

    /// <param name="AiModel">Active Anthropic model identifier.</param>
    /// <param name="AvailableModels">All selectable model identifiers.</param>
    public sealed record SettingsResponse(string AiModel, IEnumerable<string> AvailableModels);

    /// <param name="AiModel">The model identifier to switch to.</param>
    public sealed record UpdateSettingsRequest(string AiModel);
}
