using Microsoft.Extensions.Logging.Abstractions;
using Workcast.Core.Entities;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Jobs;

namespace Workcast.Jobs.Tests;

public class ScoringPipelineTests
{
    private readonly IAiProvider _aiProvider = Substitute.For<IAiProvider>();
    private readonly ISettingsRepository _settingsRepository = Substitute.For<ISettingsRepository>();
    private readonly IScraperEngine _scraperEngine = Substitute.For<IScraperEngine>();
    private readonly ScoringPipeline _sut;

    public ScoringPipelineTests()
    {
        _sut = new ScoringPipeline(
            _aiProvider,
            _settingsRepository,
            _scraperEngine,
            NullLogger<ScoringPipeline>.Instance);
    }

    [Fact]
    public async Task RunAsync_NoResume_ThrowsInvalidOperationException()
    {
        var settings = AppSettings.CreateDefault();
        _settingsRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var act = () => _sut.RunAsync("https://example.com/job/1", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no resume*");
    }

    [Fact]
    public async Task RunAsync_PageTextTooShort_ReturnsFailure()
    {
        var settings = SetupSettingsWithResume();
        _scraperEngine.RenderPageTextAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns("short");

        var outcome = await _sut.RunAsync("https://example.com/job/1", CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().Contain("less than 250 characters");
    }

    [Fact]
    public async Task RunAsync_HappyPath_ReturnsSuccessWithScores()
    {
        var settings = SetupSettingsWithResume();
        var pageText = new string('x', 300);
        _scraperEngine.RenderPageTextAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(pageText);
        _aiProvider.ScoreAdAsync(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdScoringResult
            {
                OverallScore = 85.0,
                Summary = "Strong match",
                Recommendation = "Apply now",
                Requirements =
                [
                    new AdScoringRequirementResult { Name = "C#", Category = "match", Score = 100 },
                ],
            });

        var outcome = await _sut.RunAsync("https://example.com/job/1", CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.OverallScore.Should().Be(85.0);
        outcome.Summary.Should().Be("Strong match");
        outcome.Recommendation.Should().Be("Apply now");
        outcome.Requirements.Should().HaveCount(1);
        outcome.Requirements[0].Name.Should().Be("C#");
    }

    [Fact]
    public async Task RunAsync_AiProviderThrows_ReturnsFailureWithMessage()
    {
        var settings = SetupSettingsWithResume();
        _scraperEngine.RenderPageTextAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(new string('x', 300));
        _aiProvider.ScoreAdAsync(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<AdScoringResult>(_ => throw new HttpRequestException("API timeout"));

        var outcome = await _sut.RunAsync("https://example.com/job/1", CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().Be("API timeout");
    }

    [Fact]
    public async Task RunAsync_Cancelled_ReturnsFailureWithCancelledMessage()
    {
        var settings = SetupSettingsWithResume();
        _scraperEngine.RenderPageTextAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new OperationCanceledException());

        var outcome = await _sut.RunAsync("https://example.com/job/1", CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().Be("Scoring was cancelled.");
    }

    [Fact]
    public async Task RunWithContentAsync_NoResume_ThrowsInvalidOperationException()
    {
        var settings = AppSettings.CreateDefault();
        _settingsRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        var act = () => _sut.RunWithContentAsync("job content", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no resume*");
    }

    [Fact]
    public async Task RunWithContentAsync_HappyPath_SkipsScraperAndScoresDirectly()
    {
        var settings = SetupSettingsWithResume();
        _aiProvider.ScoreAdAsync(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AdScoringResult
            {
                OverallScore = 70.0,
                Summary = "Good",
                Recommendation = "Go for it",
                Requirements = [],
            });

        var outcome = await _sut.RunWithContentAsync("long job content here", CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.OverallScore.Should().Be(70.0);
        await _scraperEngine.DidNotReceive()
            .RenderPageTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private AppSettings SetupSettingsWithResume()
    {
        var settings = AppSettings.CreateDefault();
        settings.SetResume("resume.pdf", [1, 2, 3], "application/pdf");
        _settingsRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);
        return settings;
    }
}
