using Hangfire;
using Hangfire.Dashboard;
using Microsoft.EntityFrameworkCore;
using Workcast.Infrastructure;
using Workcast.Infrastructure.Persistence;
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

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Workcast API v1"));

// NOTE: No auth filter for local dev — the default LocalRequestsOnlyAuthorizationFilter
// blocks requests arriving via Docker port-mapping. Add proper auth before any non-local deployment.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
});

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

app.UseExceptionHandler();
app.MapControllers();

app.Run();
