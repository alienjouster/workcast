using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Workcast.Infrastructure.Persistence.Interceptors;

namespace Workcast.Infrastructure.Persistence;

/// <summary>
/// Design-time factory that allows <c>dotnet ef</c> to create an <see cref="AppDbContext"/>
/// without a running ASP.NET Core host. Used exclusively for migration generation.
/// Connection string defaults to localhost for local development — the real connection string
/// is supplied at runtime via environment variables in Docker.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimestampInterceptor>();

        using var sp = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder
            .UseNpgsql("Host=localhost;Database=jobscraper;Username=jobscraper;Password=changeme")
            .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>());

        return new AppDbContext(optionsBuilder.Options);
    }
}
