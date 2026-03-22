using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Workcast.Core.Interfaces;

namespace Workcast.Api.Controllers;

/// <summary>
/// Streams real-time Workcast events to browser clients using Server-Sent Events (SSE).
/// The connection stays open until the client navigates away or closes the tab.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IEventBroadcaster _broadcaster;

    public EventsController(IEventBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Opens an SSE stream. Each event is sent as a <c>data: {json}\n\n</c> frame.
    /// The stream terminates when the client disconnects (cancellation token fires).
    /// </summary>
    [HttpGet("")]
    public async Task StreamAsync(CancellationToken ct)
    {
        // Disable Kestrel's response buffering so each SSE frame is sent to the
        // client immediately rather than waiting for the output buffer to fill.
        HttpContext.Features
            .Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()
            ?.DisableBuffering();

        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // Prevent Nginx from buffering the stream

        try
        {
            await foreach (var evt in _broadcaster.SubscribeAsync(ct).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal SSE teardown, not an error.
        }
    }
}
