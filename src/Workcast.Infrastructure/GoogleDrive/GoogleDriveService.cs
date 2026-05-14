using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using DriveFile = Google.Apis.Drive.v3.Data.File;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.GoogleDrive;

/// <summary>
/// Google Drive implementation using the official Google.Apis.Drive.v3 library.
/// Access tokens are refreshed automatically via the stored refresh token.
/// </summary>
public sealed class GoogleDriveService : IGoogleDriveService
{
    private const string DriveScope = "https://www.googleapis.com/auth/drive";

    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string _redirectUri;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(IConfiguration configuration, ILogger<GoogleDriveService> logger)
    {
        _clientId     = configuration["Google:ClientId"];
        _clientSecret = configuration["Google:ClientSecret"];
        _redirectUri  = configuration["Google:RedirectUri"] ?? "http://localhost:3000/api/google-drive/callback";
        _logger = logger;
    }

    private GoogleAuthorizationCodeFlow RequireFlow()
    {
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            throw new InvalidOperationException(
                "Google Drive is not configured. Set Google:ClientId and Google:ClientSecret.");
        return CreateFlow(_clientId, _clientSecret);
    }

    /// <inheritdoc />
    public string GetAuthorizationUrl(string state)
    {
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            throw new InvalidOperationException(
                "Google Drive is not configured. Set Google:ClientId and Google:ClientSecret.");

        return "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(_clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(_redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(DriveScope)}"
            + "&access_type=offline"
            + "&prompt=consent"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    /// <inheritdoc />
    public async Task<string> ExchangeCodeForRefreshTokenAsync(string code, CancellationToken ct)
    {
        var flow = RequireFlow();
        var response = await flow.ExchangeCodeForTokenAsync("workcast", code, _redirectUri, ct);
        if (response.RefreshToken is null)
            throw new InvalidOperationException(
                "Google did not return a refresh token. Ensure offline access is requested.");
        return response.RefreshToken;
    }

    /// <inheritdoc />
    public async Task<string> EnsureBaseFolderAsync(
        string refreshToken,
        string baseFolderPath,
        string? cachedBaseFolderId,
        CancellationToken ct)
    {
        var service = CreateDriveService(refreshToken);
        var segments = baseFolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) segments = ["jobs"];

        if (cachedBaseFolderId is not null)
        {
            try
            {
                var get = service.Files.Get(cachedBaseFolderId);
                get.Fields = "id,trashed";
                var f = await get.ExecuteAsync(ct);
                if (f.Trashed != true) return cachedBaseFolderId;
            }
            catch
            {
                _logger.LogWarning("Cached base folder {Id} no longer accessible, re-resolving.", cachedBaseFolderId);
            }
        }

        string? currentParentId = null;
        foreach (var segment in segments)
            currentParentId = await EnsureOrCreateFolderAsync(service, segment, currentParentId, ct);

        return currentParentId!;
    }

    private static async Task<string> EnsureOrCreateFolderAsync(
        DriveService service, string name, string? parentId, CancellationToken ct)
    {
        var parentClause = parentId is null ? "'root' in parents" : $"'{parentId}' in parents";
        var list = service.Files.List();
        list.Q = $"mimeType='application/vnd.google-apps.folder' and name='{Escape(name)}' and {parentClause} and trashed=false";
        list.Fields = "files(id)";
        list.PageSize = 1;
        var result = await list.ExecuteAsync(ct);
        if (result.Files?.Count > 0) return result.Files[0].Id;

        var folder = new DriveFile
        {
            Name = name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = parentId is null ? null : [parentId],
        };
        var created = await service.Files.Create(folder).ExecuteAsync(ct);
        return created.Id;
    }

    /// <inheritdoc />
    public async Task<string> CreateSubfolderAsync(
        string refreshToken,
        string parentFolderId,
        string subfolderName,
        CancellationToken ct)
    {
        var service = CreateDriveService(refreshToken);
        var folder = new DriveFile
        {
            Name = subfolderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [parentFolderId],
        };
        var created = await service.Files.Create(folder).ExecuteAsync(ct);
        return created.Id;
    }

    /// <inheritdoc />
    public async Task<string> UpsertFileAsync(
        string refreshToken,
        string folderId,
        string fileName,
        string mimeType,
        string content,
        CancellationToken ct)
    {
        var service = CreateDriveService(refreshToken);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        var list = service.Files.List();
        list.Q = $"name='{Escape(fileName)}' and '{folderId}' in parents and trashed=false";
        list.Fields = "files(id)";
        list.PageSize = 1;
        var existing = await list.ExecuteAsync(ct);

        if (existing.Files?.Count > 0)
        {
            var req = service.Files.Update(new DriveFile(), existing.Files[0].Id, new MemoryStream(bytes), mimeType);
            var status = await req.UploadAsync(ct);
            if (status.Status == Google.Apis.Upload.UploadStatus.Failed)
                throw new InvalidOperationException($"Drive file update failed: {status.Exception?.Message}");
            return existing.Files[0].Id;
        }

        var meta = new DriveFile { Name = fileName, Parents = [folderId] };
        var create = service.Files.Create(meta, new MemoryStream(bytes), mimeType);
        create.Fields = "id";
        var uploadStatus = await create.UploadAsync(ct);
        if (uploadStatus.Status == Google.Apis.Upload.UploadStatus.Failed)
            throw new InvalidOperationException($"Drive file upload failed: {uploadStatus.Exception?.Message}");
        return create.ResponseBody.Id;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GoogleAuthorizationCodeFlow CreateFlow(string clientId, string clientSecret) =>
        new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
            Scopes = [DriveScope],
        });

    private DriveService CreateDriveService(string refreshToken)
    {
        var flow = RequireFlow();
        var credential = new UserCredential(flow, "workcast", new TokenResponse { RefreshToken = refreshToken });
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Workcast",
        });
    }

    private static string Escape(string v) => v.Replace("\\", "\\\\").Replace("'", "\\'");
}
