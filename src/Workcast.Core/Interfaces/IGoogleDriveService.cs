namespace Workcast.Core.Interfaces;

/// <summary>
/// Provides Google Drive operations: OAuth2 token exchange, folder management, and file upsert.
/// </summary>
public interface IGoogleDriveService
{
    /// <summary>Returns the Google OAuth2 authorization URL to open in a browser popup.</summary>
    string GetAuthorizationUrl(string state);

    /// <summary>Exchanges an authorization code for tokens and returns the refresh token.</summary>
    Task<string> ExchangeCodeForRefreshTokenAsync(string code, CancellationToken ct);

    /// <summary>
    /// Ensures the base folder exists in Drive (creating it at root if needed).
    /// Returns the folder ID. Uses <paramref name="cachedBaseFolderId"/> first to avoid extra API calls.
    /// </summary>
    Task<string> EnsureBaseFolderAsync(
        string refreshToken,
        string baseFolderName,
        string? cachedBaseFolderId,
        CancellationToken ct);

    /// <summary>Creates a subfolder inside <paramref name="parentFolderId"/> and returns its ID.</summary>
    Task<string> CreateSubfolderAsync(
        string refreshToken,
        string parentFolderId,
        string subfolderName,
        CancellationToken ct);

    /// <summary>
    /// Upserts a file in the given Drive folder.
    /// If a file with <paramref name="fileName"/> already exists, it is replaced in-place.
    /// Returns the file ID.
    /// </summary>
    Task<string> UpsertFileAsync(
        string refreshToken,
        string folderId,
        string fileName,
        string mimeType,
        string content,
        CancellationToken ct);

    /// <summary>Returns the web view link for a Drive folder given its ID.</summary>
    static string GetFolderWebViewLink(string folderId) =>
        $"https://drive.google.com/drive/folders/{folderId}";
}
