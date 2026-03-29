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
    /// A <c>: keepalive</c> comment is sent every 15 seconds when idle to prevent
    /// proxy/intermediary timeouts from dropping the connection.
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

        // Serialize writes — HttpResponse is not thread-safe; the keepalive timer and
        // the event loop both need to write to the response concurrently.
        var writeLock = new SemaphoreSlim(1, 1);

        async Task WriteFrameAsync(string frame)
        {
            await writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await Response.WriteAsync(frame, ct).ConfigureAwait(false);
                await Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }

        // Background keepalive: sends an SSE comment every 15 s so that upstream
        // proxies (Next.js / undici, Nginx) don't time out the idle connection and
        // force an EventSource reconnect that would lose in-flight events.
        using var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var keepAliveTask = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
                while (await timer.WaitForNextTickAsync(keepAliveCts.Token).ConfigureAwait(false))
                    await WriteFrameAsync(": keepalive\n\n").ConfigureAwait(false);
            }
            catch (Exception) { /* connection dropped or cancelled — nothing to do */ }
        }, CancellationToken.None);

        try
        {
            await foreach (var evt in _broadcaster.SubscribeAsync(ct).ConfigureAwait(false))
            {
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                await WriteFrameAsync($"data: {json}\n\n").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal SSE teardown, not an error.
        }
        finally
        {
            keepAliveCts.Cancel();
            await keepAliveTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
