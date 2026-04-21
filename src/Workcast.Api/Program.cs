using System.Text.Json;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using Workcast.Core.Enums;
using Workcast.Infrastructure;
using Workcast.Infrastructure.AI;
using Workcast.Infrastructure.AI.Options;
using Workcast.Infrastructure.Persistence;
using Workcast.Api.Mcp;
using Workcast.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Workcast API", Version = "v1" });
});
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddJobs();
builder.Services.AddWorkcastMcp();

builder.Services.AddHttpClient("UrlValidation")
    .ConfigureHttpClient(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(10);
        c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; Workcast/1.0)");
    });

var app = builder.Build();


// Section 8.5: Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Validate the Anthropic API key by calling a minimal prompt on the cheapest Haiku model.
// The result is stored in the AnthropicHealthState singleton and surfaced via GET /api/status.
{
    var healthState = app.Services.GetRequiredService<AnthropicHealthState>();
    var anthropicOptions = app.Services.GetRequiredService<IOptions<AnthropicOptions>>().Value;

    try
    {
        using var pingClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        pingClient.DefaultRequestHeaders.Add("x-api-key", anthropicOptions.ApiKey);
        pingClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var payload = new
        {
            model = "claude-haiku-4-5-20251001",
            max_tokens = 5,
            messages = new[] { new { role = "user", content = "hi" } },
        };

        var response = await pingClient.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload);

        if (response.IsSuccessStatusCode)
        {
            healthState.SetHealthy();
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync();
            var errorMessage = $"HTTP {(int)response.StatusCode}";
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                {
                    errorMessage = msg.GetString() ?? errorMessage;
                }
            }
            catch { /* keep the HTTP status fallback */ }
            healthState.SetError(errorMessage);
        }
    }
    catch (Exception ex)
    {
        healthState.SetError(ex.Message);
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Workcast API v1"));

// NOTE: No auth filter for local dev — the default LocalRequestsOnlyAuthorizationFilter
// blocks requests arriving via Docker port-mapping. Add proper auth before any non-local deployment.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
});

// Register the global Hangfire state filter that syncs ScrapeRun status with Hangfire.
GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<ScrapeRunStateFilter>());

// Register recurring system jobs
var scheduler = app.Services.GetRequiredService<Workcast.Infrastructure.Scheduling.HangfireJobScheduler>();
scheduler.AddOrUpdateRecurring<StaleRunCleanupJob>(
    "stale-run-cleanup",
    x => x.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *"); // every 5 minutes

scheduler.AddOrUpdateRecurring<AdCleanupJob>(
    "ad-cleanup",
    x => x.ExecuteAsync(CancellationToken.None),
    "0 2 * * *"); // daily at 02:00 UTC

// Re-register all existing board recurring jobs to update the stored Hangfire method signature.
// Required after adding PerformContext to ScrapeJobRunner.ExecuteAsync — boards whose recurring
// jobs were last registered with the old 3-parameter signature would fail at invocation time
// because Hangfire resolves the method by name + parameter types at execution time.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var boards = await db.JobBoards
        .Where(b => b.ScraperConfig != null && b.Status != BoardStatus.Paused)
        .ToListAsync();

    foreach (var board in boards)
        scheduler.AddOrUpdateRecurring<ScrapeJobRunner>(
            $"scrape-{board.Id}",
            j => j.ExecuteAsync(board.Id, TriggerSource.Scheduler, null, CancellationToken.None),
            board.ScheduleCron);
}

app.UseHttpMetrics();  // captures per-route request count, duration, and in-flight requests
app.UseExceptionHandler();
app.MapControllers();
app.MapMcp("/mcp");
app.MapMetrics("/metrics");  // exposes the Prometheus scrape endpoint

app.Run();
