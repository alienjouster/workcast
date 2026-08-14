using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;
using Workcast.Core.Interfaces;

namespace Workcast.Api.Controllers;

[ApiController]
[Route("api/google-drive")]
public sealed class GoogleDriveController : ControllerBase
{
    private readonly IGoogleDriveService _driveService;
    private readonly ISettingsRepository _settingsRepository;

    public GoogleDriveController(IGoogleDriveService driveService, ISettingsRepository settingsRepository)
    {
        _driveService = driveService;
        _settingsRepository = settingsRepository;
    }

    /// <summary>Returns the Google OAuth2 authorization URL.</summary>
    [HttpGet("auth-url")]
    [ProducesResponseType(typeof(AuthUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetAuthUrl()
    {
        try
        {
            return Ok(new AuthUrlResponse(_driveService.GetAuthorizationUrl("workcast-drive")));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title  = "Google Drive not configured",
                Detail = ex.Message,
            });
        }
    }

    /// <summary>
    /// OAuth2 callback: exchanges the authorization code for a refresh token, stores it,
    /// and returns HTML that notifies the opener popup then closes itself.
    /// </summary>
    [HttpGet("callback")]
    [Produces("text/html")]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code, [FromQuery] string? error, CancellationToken ct)
    {
        if (error is not null || code is null)
            return Content(CallbackHtml(false, error ?? "No code returned"), "text/html");

        try
        {
            var refreshToken = await _driveService.ExchangeCodeForRefreshTokenAsync(code, ct);
            var settings = await _settingsRepository.GetAsync(ct);
            settings.SetGoogleDriveRefreshToken(refreshToken);
            await _settingsRepository.SaveAsync(ct);
            return Content(CallbackHtml(true), "text/html");
        }
        catch (Exception ex)
        {
            return Content(CallbackHtml(false, ex.Message), "text/html");
        }
    }

    /// <summary>Disconnects Google Drive by clearing the stored refresh token.</summary>
    [HttpDelete("connection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DisconnectAsync(CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        settings.ClearGoogleDriveRefreshToken();
        await _settingsRepository.SaveAsync(ct);
        return NoContent();
    }

    /// <summary>Updates the base folder path used for new application saves.</summary>
    [HttpPut("base-path")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateBasePathAsync(
        [FromBody] UpdateBasePathRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BasePath))
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Invalid base path",
                Detail = "BasePath must not be empty.",
            });

        var settings = await _settingsRepository.GetAsync(ct);
        settings.SetGoogleDriveBasePath(request.BasePath.Trim());
        await _settingsRepository.SaveAsync(ct);
        return NoContent();
    }

    private static string CallbackHtml(bool success, string? message = null)
    {
        var safeMessage = JavaScriptEncoder.Default.Encode(message ?? "Unknown error");
        var payload = success
            ? "{ type: 'google-drive-connected' }"
            : "{ type: 'google-drive-error', message: '" + safeMessage + "' }";
        var bodyText = success
            ? "Connected. You may close this window."
            : "Error: " + System.Net.WebUtility.HtmlEncode(message ?? "");

        return
            "<!DOCTYPE html><html><head><title>Google Drive</title></head><body>\n" +
            "<script>\n" +
            "  try { if (window.opener) { window.opener.postMessage(" + payload + ", window.location.origin); } } catch(e) {}\n" +
            "  window.close();\n" +
            "</script>\n" +
            "<p>" + System.Net.WebUtility.HtmlEncode(bodyText) + "</p>\n" +
            "</body></html>";
    }

    public sealed record AuthUrlResponse(string Url);
    public sealed record UpdateBasePathRequest(string BasePath);
}
