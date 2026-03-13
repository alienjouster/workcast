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
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));

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

app.UseExceptionHandler();
app.MapControllers();

app.Run();
