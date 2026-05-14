using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.AI;
using Workcast.Infrastructure.AI.Options;
using Workcast.Infrastructure.Events;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Persistence.Interceptors;
using Workcast.Infrastructure.Observability;
using Workcast.Infrastructure.Scheduling;
using Workcast.Infrastructure.GoogleDrive;
using Workcast.Infrastructure.Scraping;

namespace Workcast.Infrastructure;

/// <summary>
/// Extension methods for registering all Workcast.Infrastructure services with the DI container.
/// Called once from <c>Program.cs</c> in Workcast.Api.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the full infrastructure layer: EF Core, Playwright, Claude AI provider,
    /// HTML cleaning, AI extraction orchestration, Hangfire storage, and the job scheduler.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration (appsettings + environment variables).</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddAiServices(configuration);
        services.AddScrapingServices();
        services.AddHangfireServices(configuration);

        // EventBroadcaster must be Singleton so Hangfire jobs and the SSE controller
        // share the same in-memory channel registry.
        services.AddSingleton<IEventBroadcaster, EventBroadcaster>();

        services.AddHostedService<HangfireMetricsService>();

        services.AddScoped<IGoogleDriveService, GoogleDriveService>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // TimestampInterceptor is registered as Singleton so EF can resolve it during
        // DbContext construction.
        services.AddSingleton<TimestampInterceptor>();

        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAdScoringRepository, AdScoringRepository>();
        services.AddScoped<IInterviewDrillRepository, InterviewDrillRepository>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException(
                    "Connection string 'Default' is not configured. " +
                    "Set ConnectionStrings__Default in environment or appsettings.json.");

            options
                .UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());
        });

        return services;
    }

    private static IServiceCollection AddAiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AnthropicOptions>(
            configuration.GetSection(AnthropicOptions.SectionName));

        // Holds the result of the startup API key health check.
        services.AddSingleton<AnthropicHealthState>();

        // HttpClient for Claude API — base address and auth header configured here.
        services.AddHttpClient<ClaudeAiProvider>((sp, client) =>
        {
            var apiKey = configuration[$"{AnthropicOptions.SectionName}:ApiKey"]
                ?? string.Empty;

            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        // IAiProvider resolves to the typed-client registration above.
        // Do NOT add a second AddScoped here — that would inject a plain HttpClient with no headers.
        services.AddScoped<IAiProvider>(sp => sp.GetRequiredService<ClaudeAiProvider>());

        // Named HttpClient used by AdScoringJob to fetch job ad detail pages.
        services.AddHttpClient("JobAdFetcher", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (compatible; Workcast/1.0; +https://workcast.local)");
        });

        // HtmlCleaningService is Singleton — stateless, all state is compiled regexes.
        services.AddSingleton<HtmlCleaningService>();

        // AiExtractionService is Scoped — orchestrates Scoped dependencies.
        services.AddScoped<IJobBoardAnalyzer, AiExtractionService>();

        return services;
    }

    private static IServiceCollection AddScrapingServices(this IServiceCollection services)
    {
        // PlaywrightScraperEngine is Singleton — the browser instance is expensive to create.
        // Implements IAsyncDisposable so the DI container disposes it cleanly on shutdown.
        services.AddSingleton<IScraperEngine, PlaywrightScraperEngine>();

        return services;
    }

    private static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is required for Hangfire storage. " +
                "Set ConnectionStrings__Default in environment or appsettings.json.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.ServerName = "workcast-api";
            // Process "critical" before "default": scoring jobs (user-initiated) are picked up
            // ahead of scraping jobs (background). Workers drain critical before touching default.
            options.Queues = ["critical", "default"];
        });

        // HangfireJobScheduler is Singleton — wraps Hangfire's static API.
        services.AddSingleton<HangfireJobScheduler>();

        return services;
    }
}
