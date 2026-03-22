using Microsoft.EntityFrameworkCore;
using Workcast.Core.Entities;
using Workcast.Infrastructure.Persistence.Configurations;


namespace Workcast.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core database context for the Workcast platform.
/// Applies all entity configurations and registers the timestamp interceptor via
/// <see cref="Workcast.Infrastructure.Persistence.Interceptors.TimestampInterceptor"/>,
/// which is injected in <see cref="Workcast.Infrastructure.DependencyInjection"/>.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new <see cref="AppDbContext"/> with the given options.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>All registered job boards.</summary>
    public DbSet<JobBoard> JobBoards => Set<JobBoard>();

    /// <summary>All scraped job advertisements.</summary>
    public DbSet<JobAd> JobAds => Set<JobAd>();

    /// <summary>All scrape run records.</summary>
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();

    /// <summary>Global application settings (single row).</summary>
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    /// <summary>AI scoring results for job ads.</summary>
    public DbSet<AdScoring> AdScorings => Set<AdScoring>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new JobBoardConfiguration());
        modelBuilder.ApplyConfiguration(new JobAdConfiguration());
        modelBuilder.ApplyConfiguration(new ScrapeRunConfiguration());
        modelBuilder.ApplyConfiguration(new AppSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AdScoringConfiguration());
    }
}
