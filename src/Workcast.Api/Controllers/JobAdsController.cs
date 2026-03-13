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

        // Decode cursor: encodes "ScrapedAt_ticks|Id" as a Base64 URL-safe string.
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

            (cursorScrapedAt, cursorId) = decoded.Value;
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

        // Apply cursor: return ads with ScrapedAt older than cursor, or same ScrapedAt with smaller Id.
        if (cursorScrapedAt.HasValue && cursorId.HasValue)
        {
            var cursorTs = cursorScrapedAt.Value;
            var cursorGuid = cursorId.Value;
            query = query.Where(a =>
                a.ScrapedAt < cursorTs ||
                (a.ScrapedAt == cursorTs && a.Id.CompareTo(cursorGuid) < 0));
        }

        // Fetch one extra item to determine if a next page exists.
        var items = await query
            .OrderByDescending(a => a.ScrapedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items = items.Take(limit).ToList();
            var last = items[^1];
            nextCursor = EncodeCursor(last.ScrapedAt, last.Id);
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
    /// Encodes a cursor from ScrapedAt and Id as a Base64 URL-safe string.
    /// Format: "{ScrapedAt.Ticks}|{Id}" encoded as UTF-8 Base64.
    /// </summary>
    private static string EncodeCursor(DateTimeOffset scrapedAt, Guid id)
    {
        var raw = $"{scrapedAt.UtcTicks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Decodes a cursor string back to its component parts.
    /// Returns null if the cursor is malformed.
    /// </summary>
    private static (DateTimeOffset ScrapedAt, Guid Id)? DecodeCursor(string cursor)
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
            if (parts.Length != 2)
            {
                return null;
            }

            if (!long.TryParse(parts[0], out var ticks))
            {
                return null;
            }

            if (!Guid.TryParse(parts[1], out var id))
            {
                return null;
            }

            return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch
        {
            return null;
        }
    }
}
