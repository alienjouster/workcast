using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Controllers;

/// <summary>
/// Exposes lightweight runtime status used by the frontend (e.g. job-processing indicator,
/// unread ad count).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatusController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var isProcessing = monitoring.ProcessingCount() > 0;
        var unreadCount = await _db.JobAds.CountAsync(a => !a.IsRead && !a.IsTrashed, ct);
        return Ok(new { isProcessing, unreadCount });
    }
}
