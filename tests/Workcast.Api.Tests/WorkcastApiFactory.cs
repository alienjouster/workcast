using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Workcast.Core.Interfaces;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Tests;

public class WorkcastApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "WorkcastTest_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var efDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("EntityFramework") == true
                         || d.ServiceType.FullName?.Contains("DbContext") == true
                         || d.ImplementationType?.FullName?.Contains("EntityFramework") == true
                         || d.ImplementationType?.FullName?.Contains("Npgsql") == true)
                .ToList();
            foreach (var d in efDescriptors)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.RemoveAll<IScraperEngine>();
            services.AddSingleton<IScraperEngine>(Substitute.For<IScraperEngine>());

            services.RemoveAll<IAiProvider>();
            services.AddScoped<IAiProvider>(_ => Substitute.For<IAiProvider>());

            services.RemoveAll<IGoogleDriveService>();
            services.AddScoped<IGoogleDriveService>(_ => Substitute.For<IGoogleDriveService>());

            services.RemoveAll<IAnthropicModelsService>();
            services.AddScoped<IAnthropicModelsService>(_ => Substitute.For<IAnthropicModelsService>());

            services.AddHangfire(config => config.UseInMemoryStorage());
        });
    }
}
