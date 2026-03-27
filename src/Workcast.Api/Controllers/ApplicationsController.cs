using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;
using Workcast.Jobs;

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

    private const int MinJobAdContentLength = 250;

    private readonly AppDbContext _db;
    private readonly IScraperEngine _scraperEngine;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;

    /// <summary>Initializes a new instance of <see cref="ApplicationsController"/>.</summary>
    public ApplicationsController(
        AppDbContext db,
        IScraperEngine scraperEngine,
        ISettingsRepository settingsRepository,
        IBackgroundJobClient backgroundJobClient)
    {
        _db = db;
        _scraperEngine = scraperEngine;
        _settingsRepository = settingsRepository;
        _backgroundJobClient = backgroundJobClient;
    }

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

        // Fetch and store the full job ad page content.
        var content = await FetchJobAdContentAsync(ad.Url, ct);
        if (content is not null)
        {
            application.UpdateJobAdContent(content);
            await _db.SaveChangesAsync(ct);
        }

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

    /// <summary>Updates the stored job ad page content for an application.</summary>
    [HttpPatch("{id:guid}/job-ad-content")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateJobAdContentAsync(
        Guid id,
        [FromBody] UpdateJobAdContentRequest request,
        CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.UpdateJobAdContent(request.Content);
        await _db.SaveChangesAsync(ct);
        return Ok(application.ToResponse());
    }

    /// <summary>
    /// Enqueues a scoring job for the given application and returns 202 Accepted immediately.
    /// The result will be delivered via SSE (<c>applicationScoringCompleted</c> event) when done.
    /// Returns 422 if no resume has been uploaded.
    /// </summary>
    [HttpPost("{id:guid}/scoring")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RunScoringAsync(Guid id, CancellationToken ct)
    {
        var settings = await _settingsRepository.GetAsync(ct);
        if (!settings.HasResume)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "No resume uploaded",
                Detail = "Upload a resume in Settings before running scoring.",
            });
        }

        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.SetScoringPending();
        await _db.SaveChangesAsync(ct);

        _backgroundJobClient.Enqueue<ApplicationScoringJob>(j => j.ExecuteAsync(id, CancellationToken.None));
        return Accepted();
    }

    /// <summary>
    /// Clears a stuck <c>isScoringPending</c> flag, allowing the user to re-trigger scoring.
    /// Safe to call even if scoring is not currently pending.
    /// </summary>
    [HttpDelete("{id:guid}/scoring")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelScoringAsync(Guid id, CancellationToken ct)
    {
        var application = await _db.Applications.FindAsync(new object[] { id }, ct);
        if (application is null) return NotFound();

        application.ClearScoringPending();
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders the job ad page via Playwright and returns sanitized HTML that preserves
    /// the visual structure (headings, bold, italic, lists, inline styles) while stripping
    /// scripts, navigation, images, and dangerous attributes.
    /// Returns null if rendering fails or the extracted text content is shorter than
    /// <see cref="MinJobAdContentLength"/>.
    /// </summary>
    private async Task<string?> FetchJobAdContentAsync(string url, CancellationToken ct)
    {
        try
        {
            var html = await _scraperEngine.RenderPageAsync(url, ct: ct);
            var sanitized = SanitizePageHtml(html);
            // Measure meaningful text length by stripping tags from the sanitized output.
            var textLength = Regex.Replace(sanitized, @"<[^>]+>", "").Trim().Length;
            return textLength >= MinJobAdContentLength ? sanitized : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sanitizes raw page HTML for storage and display in the rich-text Job Ad Detail panel.
    /// Keeps structural and inline-styled markup; removes everything that is either dangerous
    /// (scripts, event handlers) or irrelevant to the job description (nav, images, forms).
    /// </summary>
    private static string SanitizePageHtml(string html)
    {
        // ── 1. Extract the most focused content region available ─────────────
        // Prefer <main> or <article> over the full <body> to skip page chrome.
        var content =
            TryExtractElement(html, "main") ??
            TryExtractElement(html, "article") ??
            TryExtractElement(html, "body") ??
            html;

        // ── 2. Remove blocks that are never useful for job-description display ─
        content = Regex.Replace(
            content,
            @"<(script|style|noscript|nav|header|footer|aside|form|iframe|svg|figure)[^>]*>[\s\S]*?</(script|style|noscript|nav|header|footer|aside|form|iframe|svg|figure)>",
            "",
            RegexOptions.IgnoreCase);

        // Remove self-closing / void elements that add no readable value.
        content = Regex.Replace(
            content,
            @"<(img|input|select|textarea|button|link|meta|canvas|video|audio|source|track|embed|object)[^>]*/?>",
            "",
            RegexOptions.IgnoreCase);

        // ── 3. Strip dangerous attributes; keep only safe ones ────────────────
        // Allow: style, href (filtered below), target, colspan, rowspan, align.
        content = Regex.Replace(
            content,
            @"\s(?:class|id|on\w+|data-\w+|aria-\w+|role|tabindex|name|action|method|enctype|autocomplete)[^=]*=(?:""[^""]*""|'[^']*'|[^\s>]*)",
            "",
            RegexOptions.IgnoreCase);

        // Remove javascript: hrefs.
        content = Regex.Replace(
            content,
            @"href\s*=\s*(?:""javascript:[^""]*""|'javascript:[^']*')",
            "",
            RegexOptions.IgnoreCase);

        // ── 4. Normalise whitespace ───────────────────────────────────────────
        content = Regex.Replace(content, @"[ \t]{2,}", " ");
        content = Regex.Replace(content, @"(\s*\n\s*){3,}", "\n\n");

        return content.Trim();
    }

    /// <summary>
    /// Extracts the inner content of the first occurrence of <paramref name="tag"/> in
    /// <paramref name="html"/>, or null if the tag is not present.
    /// </summary>
    private static string? TryExtractElement(string html, string tag)
    {
        var match = Regex.Match(
            html,
            $@"<{tag}(?:\s[^>]*)?>(?<content>[\s\S]*?)</{tag}>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["content"].Value : null;
    }

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

/// <summary>Request body for updating the stored job ad page content.</summary>
public record UpdateJobAdContentRequest
{
    /// <summary>Gets the new content. Null clears the stored content.</summary>
    public string? Content { get; init; }
}
