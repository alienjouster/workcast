using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Controllers;

/// <summary>
/// Provides read and delete access to scraped job advertisements.
/// </summary>
[ApiController]
[Route("api/job-ads")]
public sealed class JobAdsController : ControllerBase
{
    private const string ERROR_TYPE_BASE = "https://workcast.local/errors/";

    private readonly AppDbContext _db;
    private readonly ILogger<JobAdsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="JobAdsController"/>.
    /// </summary>
    public JobAdsController(AppDbContext db, ILogger<JobAdsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paginated list of job ads, optionally filtered by board, search term, and active status.
    /// Uses cursor-based pagination ordered by ScrapedAt DESC, then Id DESC.
    /// </summary>
    /// <param name="boardId">Optional filter: only ads from this board.</param>
    /// <param name="search">Optional filter: case-insensitive substring match against title, company, or location.</param>
    /// <param name="isActive">Optional filter: true returns only active ads, false only inactive.</param>
    /// <param name="cursor">Optional pagination cursor returned by a previous response.</param>
    /// <param name="limit">Maximum number of items per page. Default 50, max 200.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<JobAdResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? boardId,
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        // Decode cursor: encodes "IsPinned|ScrapedAt_ticks|Id" as a Base64 URL-safe string.
        bool? cursorIsPinned = null;
        DateTimeOffset? cursorScrapedAt = null;
        Guid? cursorId = null;

        if (!string.IsNullOrEmpty(cursor))
        {
            var decoded = DecodeCursor(cursor);
            if (decoded is null)
            {
                return Problem(
                    type: $"{ERROR_TYPE_BASE}invalid-cursor",
                    title: "Invalid Cursor",
                    statusCode: StatusCodes.Status400BadRequest,
                    detail: "The provided cursor value is invalid or corrupted.");
            }

            (cursorIsPinned, cursorScrapedAt, cursorId) = decoded.Value;
        }

        var query = _db.JobAds.AsQueryable();

        if (boardId.HasValue)
        {
            query = query.Where(a => a.JobBoardId == boardId.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var lower = search.ToLowerInvariant();
            query = query.Where(a =>
                (a.Title != null && a.Title.ToLower().Contains(lower)) ||
                (a.Company != null && a.Company.ToLower().Contains(lower)) ||
                (a.Location != null && a.Location.ToLower().Contains(lower)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value);
        }

        // Apply cursor: pinned items sort before unpinned, then ScrapedAt DESC, then Id DESC.
        if (cursorIsPinned.HasValue && cursorScrapedAt.HasValue && cursorId.HasValue)
        {
            var cPinned = cursorIsPinned.Value;
            var cTs = cursorScrapedAt.Value;
            var cGuid = cursorId.Value;
            query = query.Where(a =>
                (!a.IsPinned && cPinned) ||
                (a.IsPinned == cPinned && a.ScrapedAt < cTs) ||
                (a.IsPinned == cPinned && a.ScrapedAt == cTs && a.Id.CompareTo(cGuid) < 0));
        }

        // Fetch one extra item to determine if a next page exists.
        var items = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.ScrapedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items = items.Take(limit).ToList();
            var last = items[^1];
            nextCursor = EncodeCursor(last.IsPinned, last.ScrapedAt, last.Id);
        }

        var response = new PagedResponse<JobAdResponse>
        {
            Items = items.Select(a => a.ToResponse()).ToList(),
            NextCursor = nextCursor,
            Count = items.Count,
        };

        return Ok(response);
    }

    /// <summary>
    /// Returns a single job ad by ID.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        return Ok(ad.ToResponse());
    }

    /// <summary>
    /// Pins a job ad so it appears at the top of all job ad lists.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{id:guid}/pin")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PinAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        ad.Pin();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Pinned job ad {AdId}.", id);

        return Ok(ad.ToResponse());
    }

    /// <summary>
    /// Unpins a job ad, returning it to its natural sort position.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{id:guid}/unpin")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpinAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        ad.Unpin();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Unpinned job ad {AdId}.", id);

        return Ok(ad.ToResponse());
    }

    /// <summary>
    /// Marks a job ad as read.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReadAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        ad.MarkRead();
        await _db.SaveChangesAsync(ct);

        return Ok(ad.ToResponse());
    }

    /// <summary>
    /// Marks a job ad as unread.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPatch("{id:guid}/unread")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkUnreadAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        ad.MarkUnread();
        await _db.SaveChangesAsync(ct);

        return Ok(ad.ToResponse());
    }

    /// <summary>
    /// Returns the total count of unread job ads.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCountAsync(CancellationToken ct)
    {
        var count = await _db.JobAds.CountAsync(a => !a.IsRead, ct);
        return Ok(count);
    }

    /// <summary>
    /// Marks all job ads as read, optionally scoped to a specific board.
    /// </summary>
    /// <param name="boardId">Optional filter: only ads from this board.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllReadAsync(
        [FromQuery] Guid? boardId,
        CancellationToken ct)
    {
        var query = _db.JobAds.Where(a => !a.IsRead);

        if (boardId.HasValue)
        {
            query = query.Where(a => a.JobBoardId == boardId.Value);
        }

        await query.ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true), ct);

        return NoContent();
    }

    /// <summary>
    /// Hard-deletes a job ad by ID.
    /// </summary>
    /// <param name="id">The job ad identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);

        if (ad is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Job ad '{id}' was not found.");
        }

        _db.JobAds.Remove(ad);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted job ad {AdId}.", id);

        return NoContent();
    }

    /// <summary>
    /// Encodes a cursor from IsPinned, ScrapedAt, and Id as a Base64 URL-safe string.
    /// Format: "{IsPinned}|{ScrapedAt.Ticks}|{Id}" encoded as UTF-8 Base64.
    /// </summary>
    private static string EncodeCursor(bool isPinned, DateTimeOffset scrapedAt, Guid id)
    {
        var raw = $"{(isPinned ? 1 : 0)}|{scrapedAt.UtcTicks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Decodes a cursor string back to its component parts.
    /// Returns null if the cursor is malformed.
    /// </summary>
    private static (bool IsPinned, DateTimeOffset ScrapedAt, Guid Id)? DecodeCursor(string cursor)
    {
        try
        {
            // Restore standard Base64 padding and characters.
            var base64 = cursor
                .Replace('-', '+')
                .Replace('_', '/');

            var padded = (base64.Length % 4) switch
            {
                2 => base64 + "==",
                3 => base64 + "=",
                _ => base64,
            };

            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('|');
            if (parts.Length != 3)
            {
                return null;
            }

            if (!int.TryParse(parts[0], out var pinnedInt) || pinnedInt is not (0 or 1))
            {
                return null;
            }

            if (!long.TryParse(parts[1], out var ticks))
            {
                return null;
            }

            if (!Guid.TryParse(parts[2], out var id))
            {
                return null;
            }

            return (pinnedInt == 1, new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch
        {
            return null;
        }
    }
}
