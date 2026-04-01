using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Requests;
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
        [FromQuery] Guid[]? boardIds,
        [FromQuery] Guid[]? excludeBoardIds,
        [FromQuery] string[]? titles,
        [FromQuery] string[]? excludeTitles,
        [FromQuery] string[]? locations,
        [FromQuery] string[]? excludeLocations,
        [FromQuery] string[]? companies,
        [FromQuery] string[]? excludeCompanies,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isRead,
        [FromQuery] bool? isPinned,
        [FromQuery] string? cursor,
        [FromQuery] bool trashed = false,
        [FromQuery] double? minScore = null,
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

        var query = _db.JobAds.AsQueryable().Where(a => a.IsTrashed == trashed);

        if (boardIds?.Length > 0)
            query = query.Where(a => boardIds.Contains(a.JobBoardId));

        if (excludeBoardIds?.Length > 0)
            query = query.Where(a => !excludeBoardIds.Contains(a.JobBoardId));

        if (titles?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Title, titles);

        if (excludeTitles?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Title, excludeTitles);

        if (locations?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Location, locations);

        if (excludeLocations?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Location, excludeLocations);

        if (companies?.Length > 0)
            query = ApplyPartialMatchFilter(query, a => a.Company, companies);

        if (excludeCompanies?.Length > 0)
            query = ApplyPartialMatchExcludeFilter(query, a => a.Company, excludeCompanies);

        if (isActive.HasValue)
            query = query.Where(a => a.IsActive == isActive.Value);

        if (isRead.HasValue)
            query = query.Where(a => a.IsRead == isRead.Value);

        if (isPinned.HasValue)
            query = query.Where(a => a.IsPinned == isPinned.Value);

        if (minScore.HasValue)
        {
            var threshold = minScore.Value;
            query = query.Where(a => _db.Set<Workcast.Core.Entities.AdScoring>()
                .Any(s => s.JobAdId == a.Id && s.OverallScore >= threshold));
        }

        // Snapshot the filtered query before the cursor predicate is applied.
        // TotalCount reflects the full filtered set regardless of which page is being fetched.
        var countQuery = query;

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

        var totalCount = await countQuery.CountAsync(ct);

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

        // Batch-fetch scores for the current page to avoid N+1 queries.
        var adIds = items.Select(a => a.Id).ToList();
        var scores = await _db.Set<Workcast.Core.Entities.AdScoring>()
            .Where(s => adIds.Contains(s.JobAdId))
            .Select(s => new { s.JobAdId, s.OverallScore })
            .ToDictionaryAsync(s => s.JobAdId, s => s.OverallScore, ct);

        var response = new PagedResponse<JobAdResponse>
        {
            Items = items.Select(a => a.ToResponse(scores.TryGetValue(a.Id, out var sc) ? sc : null)).ToList(),
            NextCursor = nextCursor,
            Count = items.Count,
            TotalCount = totalCount,
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

    /// <summary>Moves a job ad to the trash bin (soft delete).</summary>
    [HttpPatch("{id:guid}/trash")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrashAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);
        if (ad is null)
            return Problem(type: $"{ERROR_TYPE_BASE}not-found", title: "Not Found",
                statusCode: StatusCodes.Status404NotFound, detail: $"Job ad '{id}' was not found.");

        ad.Trash();
        await _db.SaveChangesAsync(ct);
        return Ok(ad.ToResponse());
    }

    /// <summary>Restores a job ad from the trash bin back to the main list.</summary>
    [HttpPatch("{id:guid}/restore")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);
        if (ad is null)
            return Problem(type: $"{ERROR_TYPE_BASE}not-found", title: "Not Found",
                statusCode: StatusCodes.Status404NotFound, detail: $"Job ad '{id}' was not found.");

        ad.Restore();
        await _db.SaveChangesAsync(ct);
        return Ok(ad.ToResponse());
    }

    /// <summary>Sets or clears the personal note for a job ad.</summary>
    [HttpPatch("{id:guid}/note")]
    [ProducesResponseType(typeof(JobAdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetNoteAsync(Guid id, [FromBody] SetNoteRequest req, CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { id }, ct);
        if (ad is null)
            return Problem(type: $"{ERROR_TYPE_BASE}not-found", title: "Not Found",
                statusCode: StatusCodes.Status404NotFound, detail: $"Job ad '{id}' was not found.");

        ad.SetNote(req.Note);
        await _db.SaveChangesAsync(ct);
        return Ok(ad.ToResponse());
    }

    // ── Bulk actions ────────────────────────────────────────────────────────────

    /// <summary>Pins all specified job ads.</summary>
    [HttpPost("bulk/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkPinAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsPinned, true), ct);
        return NoContent();
    }

    /// <summary>Unpins all specified job ads.</summary>
    [HttpPost("bulk/unpin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkUnpinAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsPinned, false), ct);
        return NoContent();
    }

    /// <summary>Marks all specified job ads as read.</summary>
    [HttpPost("bulk/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkMarkReadAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true), ct);
        return NoContent();
    }

    /// <summary>Marks all specified job ads as unread.</summary>
    [HttpPost("bulk/unread")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkMarkUnreadAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, false), ct);
        return NoContent();
    }

    /// <summary>Moves all specified job ads to the trash bin.</summary>
    [HttpPost("bulk/trash")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkTrashAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsTrashed, true), ct);
        return NoContent();
    }

    /// <summary>Restores all specified job ads from the trash bin.</summary>
    [HttpPost("bulk/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkRestoreAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsTrashed, false), ct);
        return NoContent();
    }

    /// <summary>Hard-deletes all specified job ads.</summary>
    [HttpPost("bulk/delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkDeleteAsync([FromBody] BulkAdActionRequest req, CancellationToken ct)
    {
        await _db.JobAds.Where(a => req.Ids.Contains(a.Id))
            .ExecuteDeleteAsync(ct);
        return NoContent();
    }

    // ── Hard delete ─────────────────────────────────────────────────────────────

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

    /// <summary>Returns distinct title values from non-trashed ads, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-titles")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctTitlesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.JobAds.Where(a => !a.IsTrashed && a.Title != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Title, [q]);
        var results = await query
            .Select(a => a.Title!)
            .Distinct()
            .OrderBy(t => t)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    /// <summary>Returns distinct location values from non-trashed ads, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-locations")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctLocationsAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.JobAds.Where(a => !a.IsTrashed && a.Location != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Location, [q]);
        var results = await query
            .Select(a => a.Location!)
            .Distinct()
            .OrderBy(l => l)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    /// <summary>Returns distinct company values from non-trashed ads, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-companies")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctCompaniesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.JobAds.Where(a => !a.IsTrashed && a.Company != null);
        if (!string.IsNullOrEmpty(q))
            query = ApplyPartialMatchFilter(query, a => a.Company, [q]);
        var results = await query
            .Select(a => a.Company!)
            .Distinct()
            .OrderBy(c => c)
            .Take(20)
            .ToListAsync(ct);
        return Ok(results);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an OR-combined WHERE clause: field LIKE '%val1%' OR field LIKE '%val2%'.
    /// Uses expression trees so EF Core can translate to SQL without client evaluation.
    /// </summary>
    private static IQueryable<Workcast.Core.Entities.JobAd> ApplyPartialMatchFilter(
        IQueryable<Workcast.Core.Entities.JobAd> query,
        Expression<Func<Workcast.Core.Entities.JobAd, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Workcast.Core.Entities.JobAd), "a");
        var prop = Expression.Property(param, ((MemberExpression)selector.Body).Member.Name);
        var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod("Contains", [typeof(string)])!;

        Expression? combined = null;
        foreach (var value in values)
        {
            var lower = value.ToLowerInvariant();
            var notNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(string)));
            var cond = Expression.AndAlso(
                notNull,
                Expression.Call(Expression.Call(prop, toLower), contains, Expression.Constant(lower)));
            combined = combined is null ? cond : Expression.OrElse(combined, cond);
        }

        return combined is null ? query : query.Where(Expression.Lambda<Func<Workcast.Core.Entities.JobAd, bool>>(combined, param));
    }

    /// <summary>
    /// Builds a NOT-OR WHERE clause: NOT (field LIKE '%val1%' OR field LIKE '%val2%').
    /// Null fields pass through (an ad with no location is not excluded by a location exclusion).
    /// </summary>
    private static IQueryable<Workcast.Core.Entities.JobAd> ApplyPartialMatchExcludeFilter(
        IQueryable<Workcast.Core.Entities.JobAd> query,
        Expression<Func<Workcast.Core.Entities.JobAd, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Workcast.Core.Entities.JobAd), "a");
        var prop = Expression.Property(param, ((MemberExpression)selector.Body).Member.Name);
        var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod("Contains", [typeof(string)])!;

        Expression? anyMatch = null;
        foreach (var value in values)
        {
            var lower = value.ToLowerInvariant();
            var notNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(string)));
            var cond = Expression.AndAlso(
                notNull,
                Expression.Call(Expression.Call(prop, toLower), contains, Expression.Constant(lower)));
            anyMatch = anyMatch is null ? cond : Expression.OrElse(anyMatch, cond);
        }

        return anyMatch is null ? query : query.Where(Expression.Lambda<Func<Workcast.Core.Entities.JobAd, bool>>(Expression.Not(anyMatch), param));
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
