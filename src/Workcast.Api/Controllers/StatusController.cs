using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Workcast.Infrastructure.AI;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Controllers;

/// <summary>
/// Exposes lightweight runtime status used by the frontend (e.g. job-processing indicator,
/// unread ad count, Anthropic API key health).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AnthropicHealthState _anthropicHealth;

    public StatusController(AppDbContext db, AnthropicHealthState anthropicHealth)
    {
        _db = db;
        _anthropicHealth = anthropicHealth;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var isProcessing = monitoring.ProcessingCount() > 0;
        var unreadCount = await _db.JobAds.CountAsync(a => !a.IsRead && !a.IsTrashed, ct);
        var aiKeyError = _anthropicHealth.IsHealthy ? null : _anthropicHealth.ErrorMessage;
        return Ok(new { isProcessing, unreadCount, aiKeyError });
    }
}
