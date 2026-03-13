using Microsoft.AspNetCore.Mvc;
using Workcast.Api.DTOs.Responses;
using Workcast.Api.Mapping;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Controllers;

/// <summary>
/// Provides read access to scrape run execution records.
/// </summary>
[ApiController]
[Route("api/runs")]
public sealed class ScrapeRunsController : ControllerBase
{
    private const string ERROR_TYPE_BASE = "https://workcast.local/errors/";

    private readonly AppDbContext _db;
    private readonly ILogger<ScrapeRunsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ScrapeRunsController"/>.
    /// </summary>
    public ScrapeRunsController(AppDbContext db, ILogger<ScrapeRunsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the details of a single scrape run by ID, including the full errors array.
    /// </summary>
    /// <param name="id">The scrape run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScrapeRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await _db.ScrapeRuns.FindAsync(new object[] { id }, ct);

        if (run is null)
        {
            return Problem(
                type: $"{ERROR_TYPE_BASE}not-found",
                title: "Not Found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Scrape run '{id}' was not found.");
        }

        return Ok(run.ToResponse());
    }
}
