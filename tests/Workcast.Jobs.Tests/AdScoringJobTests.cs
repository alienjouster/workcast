using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Workcast.Core.Entities;
using Workcast.Core.Events;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.Persistence;
using Workcast.Jobs;

namespace Workcast.Jobs.Tests;

public class AdScoringJobTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly IAdScoringRepository _scoringRepository = Substitute.For<IAdScoringRepository>();
    private readonly IEventBroadcaster _broadcaster = Substitute.For<IEventBroadcaster>();
    private readonly IAiProvider _aiProvider = Substitute.For<IAiProvider>();
    private readonly ISettingsRepository _settingsRepository = Substitute.For<ISettingsRepository>();
    private readonly IScraperEngine _scraperEngine = Substitute.For<IScraperEngine>();
    private readonly AdScoringJob _sut;

    public AdScoringJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        var pipeline = new ScoringPipeline(
            _aiProvider,
            _settingsRepository,
            _scraperEngine,
            NullLogger<ScoringPipeline>.Instance);

        _sut = new AdScoringJob(
            _dbContext,
            _scoringRepository,
            _broadcaster,
            pipeline,
            NullLogger<AdScoringJob>.Instance);

        var settings = AppSettings.CreateDefault();
        settings.SetResume("resume.pdf", [1, 2, 3], "application/pdf");
        _settingsRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
    }

    [Fact]
    public async Task ExecuteAsync_AdNotFound_ReturnsEarlyWithoutScoring()
    {
        var nonExistentId = Guid.NewGuid();

        await _sut.ExecuteAsync(nonExistentId);

        await _aiProvider.DidNotReceive().ScoreAdAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _broadcaster.DidNotReceive().PublishAsync(Arg.Any<WorkcastEvent>());
    }

    [Fact]
    public async Task ExecuteAsync_PipelineSucceeds_SavesScoringAndClearsPending()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.SetScoringPending();
        _dbContext.JobAds.Add(ad);
        await _dbContext.SaveChangesAsync();

        _scraperEngine.RenderPageTextAsync(ad.Url, null, Arg.Any<CancellationToken>())
            .Returns(new string('x', 300));
        _aiProvider.ScoreAdAsync(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdScoringResult
            {
                OverallScore = 90.0,
                Summary = "Great",
                Recommendation = "Apply",
                Requirements = [],
            });

        await _sut.ExecuteAsync(ad.Id);

        ad.IsScoringPending.Should().BeFalse();
        ad.LastScoringError.Should().BeNull();
        await _scoringRepository.Received(1).UpsertAsync(Arg.Any<AdScoring>(), Arg.Any<CancellationToken>());
        await _broadcaster.Received(1).PublishAsync(Arg.Is<WorkcastEvent>(e =>
            e.Type == WorkcastEvent.ScoringCompleted && e.AdId == ad.Id));
    }

    [Fact]
    public async Task ExecuteAsync_PipelineFails_SetsScoringFailedAndPublishesEvent()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.SetScoringPending();
        _dbContext.JobAds.Add(ad);
        await _dbContext.SaveChangesAsync();

        _scraperEngine.RenderPageTextAsync(ad.Url, null, Arg.Any<CancellationToken>())
            .Returns("short");

        await _sut.ExecuteAsync(ad.Id);

        ad.IsScoringPending.Should().BeFalse();
        ad.LastScoringError.Should().Contain("less than 250 characters");
        await _scoringRepository.DidNotReceive().UpsertAsync(Arg.Any<AdScoring>(), Arg.Any<CancellationToken>());
        await _broadcaster.Received(1).PublishAsync(Arg.Is<WorkcastEvent>(e =>
            e.Type == WorkcastEvent.ScoringCompleted && e.AdId == ad.Id));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
