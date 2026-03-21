using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Workcast.Api.Controllers;

/// <summary>
/// Exposes lightweight runtime status used by the frontend (e.g. job-processing indicator).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var isProcessing = monitoring.ProcessingCount() > 0;
        return Ok(new { isProcessing });
    }
}
