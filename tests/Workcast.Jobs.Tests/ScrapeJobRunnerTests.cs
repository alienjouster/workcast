using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Scheduling;
using Workcast.Jobs;

namespace Workcast.Jobs.Tests;

public class ScrapeJobRunnerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IScraperEngine _scraperEngine = Substitute.For<IScraperEngine>();
    private readonly IEventBroadcaster _broadcaster = Substitute.For<IEventBroadcaster>();
    private readonly ScrapeJobRunner _sut;

    public ScrapeJobRunnerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _sut = new ScrapeJobRunner(
            _dbContext,
            _scraperEngine,
            new HangfireJobScheduler(),
            _broadcaster,
            NullLogger<ScrapeJobRunner>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_BoardNotFound_ReturnsEarly()
    {
        var nonExistentId = Guid.NewGuid();

        await _sut.ExecuteAsync(nonExistentId);

        await _scraperEngine.DidNotReceive()
            .RenderPageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BoardPaused_ReturnsEarly()
    {
        var board = JobBoard.Create("https://example.com/jobs");
        board.Activate(CreateConfig());
        board.Pause();
        _dbContext.JobBoards.Add(board);
        await _dbContext.SaveChangesAsync();

        await _sut.ExecuteAsync(board.Id);

        await _scraperEngine.DidNotReceive()
            .RenderPageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoScraperConfig_ReturnsEarly()
    {
        var board = JobBoard.Create("https://example.com/jobs");
        _dbContext.JobBoards.Add(board);
        await _dbContext.SaveChangesAsync();

        await _sut.ExecuteAsync(board.Id);

        await _scraperEngine.DidNotReceive()
            .RenderPageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_BoardNotFound_DoesNotPublishEvents()
    {
        await _sut.ExecuteAsync(Guid.NewGuid());

        await _broadcaster.DidNotReceive().PublishAsync(Arg.Any<Core.Events.WorkcastEvent>());
    }

    [Fact]
    public async Task ExecuteAsync_BoardPaused_DoesNotCreateScrapeRun()
    {
        var board = JobBoard.Create("https://example.com/jobs");
        board.Activate(CreateConfig());
        board.Pause();
        _dbContext.JobBoards.Add(board);
        await _dbContext.SaveChangesAsync();

        await _sut.ExecuteAsync(board.Id);

        var runs = await _dbContext.ScrapeRuns.ToListAsync();
        runs.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_NoConfig_DoesNotCreateScrapeRun()
    {
        var board = JobBoard.Create("https://example.com/jobs");
        _dbContext.JobBoards.Add(board);
        await _dbContext.SaveChangesAsync();

        await _sut.ExecuteAsync(board.Id);

        var runs = await _dbContext.ScrapeRuns.ToListAsync();
        runs.Should().BeEmpty();
    }

    private static ScraperConfig CreateConfig() => new()
    {
        PaginationType = PaginationType.None,
        JobCardSelector = ".job-card",
        FieldSelectors = new FieldSelectorMap(),
        RequiresJs = false,
        SuggestedDelayMs = 1000,
        ConfidenceScore = 0.9f,
        GeneratedAt = DateTimeOffset.UtcNow,
    };

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
