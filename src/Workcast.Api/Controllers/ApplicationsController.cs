using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Core.Entities;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Controllers;

/// <summary>
/// Manages user job application files.
/// Each application is created from a job ad and carries a snapshot of the ad and any
/// scoring data at the time of creation, so it persists independently of the source ad.
/// </summary>
[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private const string ERROR_TYPE_BASE = "https://workcast.local/errors/";

    private readonly AppDbContext _db;

    /// <summary>Initializes a new instance of <see cref="ApplicationsController"/>.</summary>
    public ApplicationsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Creates a new application from a job ad, copying all available job ad and scoring data.
    /// If an application already exists for the given job ad, the existing application is returned
    /// with HTTP 200 instead of creating a duplicate.
    /// Returns HTTP 201 for a newly created application.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateApplicationRequest request,
        CancellationToken ct)
    {
        var ad = await _db.JobAds.FindAsync(new object[] { request.JobAdId }, ct);
        if (ad is null) return NotFound();

        // Return existing application if one already exists for this job ad.
        var existing = await _db.Applications
            .FirstOrDefaultAsync(a => a.JobAdId == request.JobAdId, ct);

        if (existing is not null)
            return Ok(existing.ToResponse());

        var scoring = await _db.AdScorings
            .FirstOrDefaultAsync(s => s.JobAdId == request.JobAdId, ct);

        var application = Application.CreateFromJobAd(ad, scoring);
        _db.Applications.Add(application);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/applications/{application.Id}", application.ToResponse());
    }

    /// <summary>
    /// Returns a paginated list of applications with optional filtering.
    /// Ordered by <c>CreatedAt DESC</c>, then <c>Id DESC</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string[]? titles,
        [FromQuery] string[]? excludeTitles,
        [FromQuery] string[]? locations,
        [FromQuery] string[]? excludeLocations,
        [FromQuery] string[]? companies,
        [FromQuery] string[]? excludeCompanies,
        [FromQuery] double? minScore,
        [FromQuery] bool trashed = false,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        DateTimeOffset? cursorCreatedAt = null;
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
            (cursorCreatedAt, cursorId) = decoded.Value;
        }

        var query = _db.Applications.AsQueryable()
            .Where(a => a.IsTrashed == trashed);

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
        if (minScore is not null)
            query = query.Where(a => a.OverallScore != null && a.OverallScore >= minScore);

        var totalCount = await query.CountAsync(ct);

        if (cursorCreatedAt is not null && cursorId is not null)
        {
            var cAt = cursorCreatedAt.Value;
            var cId = cursorId.Value;
            query = query.Where(a =>
                a.CreatedAt < cAt ||
                (a.CreatedAt == cAt && a.Id.CompareTo(cId) < 0));
        }

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > limit)
        {
            items.RemoveAt(limit);
            var last = items[^1];
            nextCursor = EncodeCursor(last.CreatedAt, last.Id);
        }

        return Ok(new PagedResponse<ApplicationResponse>
        {
            Items = items.Select(a => a.ToResponse()).ToList(),
            NextCursor = nextCursor,
            Count = items.Count,
            TotalCount = totalCount,
        });
    }

    /// <summary>Returns a single application by ID, or 404 if not found.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();
        return Ok(application.ToResponse());
    }

    /// <summary>Returns distinct title values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-titles")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctTitlesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Title != null);
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

    /// <summary>Returns distinct location values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-locations")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctLocationsAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Location != null);
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

    /// <summary>Returns distinct company values from non-trashed applications, optionally filtered by a partial query.</summary>
    [HttpGet("distinct-companies")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DistinctCompaniesAsync([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Applications.Where(a => !a.IsTrashed && a.Company != null);
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

    /// <summary>Moves an application to the trash bin.</summary>
    [HttpPatch("{id:guid}/trash")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TrashAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.Trash();
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Restores an application from the trash bin.</summary>
    [HttpPatch("{id:guid}/restore")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.Restore();
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>Permanently deletes an application. This action cannot be undone.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        _db.Applications.Remove(application);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IQueryable<Application> ApplyPartialMatchFilter(
        IQueryable<Application> query,
        Expression<Func<Application, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Application), "a");
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

        return combined is null ? query : query.Where(Expression.Lambda<Func<Application, bool>>(combined, param));
    }

    private static IQueryable<Application> ApplyPartialMatchExcludeFilter(
        IQueryable<Application> query,
        Expression<Func<Application, string?>> selector,
        string[] values)
    {
        var param = Expression.Parameter(typeof(Application), "a");
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

        return anyMatch is null ? query : query.Where(Expression.Lambda<Func<Application, bool>>(Expression.Not(anyMatch), param));
    }

    private static string EncodeCursor(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt.UtcTicks}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static (DateTimeOffset CreatedAt, Guid Id)? DecodeCursor(string cursor)
    {
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "=";  break;
            }
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts = raw.Split('|');
            if (parts.Length != 2) return null;
            var createdAt = new DateTimeOffset(long.Parse(parts[0]), TimeSpan.Zero);
            var id = Guid.Parse(parts[1]);
            return (createdAt, id);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Request body for creating a new application from a job ad.</summary>
public record CreateApplicationRequest
{
    /// <summary>Gets the identifier of the job ad to apply to.</summary>
    public required Guid JobAdId { get; init; }
}
