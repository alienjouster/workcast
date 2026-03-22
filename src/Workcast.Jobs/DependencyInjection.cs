using Microsoft.Extensions.DependencyInjection;

namespace Workcast.Jobs;

/// <summary>
/// Extension methods for registering Workcast.Jobs services with the DI container.
/// Hangfire resolves job instances via the registered DI activator; both job classes
/// must be registered so their constructor dependencies can be injected at execution time.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the Hangfire job classes with the DI container as transient services.
    /// Called from <c>Workcast.Api</c>'s <c>Program.cs</c> during service configuration.
    /// </summary>
    /// <param name="services">The service collection to add jobs to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobs(this IServiceCollection services)
    {
        services.AddTransient<BoardAnalysisJob>();
        services.AddTransient<ScrapeJobRunner>();
        services.AddTransient<StaleRunCleanupJob>();
        services.AddTransient<AdScoringJob>();
        return services;
    }
}
