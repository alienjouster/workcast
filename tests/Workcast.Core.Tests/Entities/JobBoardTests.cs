using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Models;

namespace Workcast.Core.Tests.Entities;

public class JobBoardTests
{
    [Fact]
    public void Create_WithDefaults_SetsStatusPendingAndHourlyCron()
    {
        var board = JobBoard.Create("https://example.com/jobs");

        board.Url.Should().Be("https://example.com/jobs");
        board.Name.Should().BeNull();
        board.Status.Should().Be(BoardStatus.Pending);
        board.ScheduleCron.Should().Be("0 * * * *");
        board.ScraperConfig.Should().BeNull();
        board.LastScrapedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithNameAndCron_SetsProvidedValues()
    {
        var board = JobBoard.Create("https://example.com", "My Board", "0 0 * * *");

        board.Name.Should().Be("My Board");
        board.ScheduleCron.Should().Be("0 0 * * *");
    }

    [Fact]
    public void Activate_SetsConfigAndStatusActive()
    {
        var board = JobBoard.Create("https://example.com");
        var config = CreateConfig();

        board.Activate(config);

        board.Status.Should().Be(BoardStatus.Active);
        board.ScraperConfig.Should().Be(config);
    }

    [Fact]
    public void SetPending_TransitionsStatusToPending()
    {
        var board = JobBoard.Create("https://example.com");
        board.Activate(CreateConfig());

        board.SetPending();

        board.Status.Should().Be(BoardStatus.Pending);
    }

    [Fact]
    public void SetError_TransitionsStatusToError()
    {
        var board = JobBoard.Create("https://example.com");

        board.SetError();

        board.Status.Should().Be(BoardStatus.Error);
    }

    [Fact]
    public void Pause_TransitionsStatusToPaused()
    {
        var board = JobBoard.Create("https://example.com");
        board.Activate(CreateConfig());

        board.Pause();

        board.Status.Should().Be(BoardStatus.Paused);
    }

    [Fact]
    public void Resume_TransitionsStatusToActive()
    {
        var board = JobBoard.Create("https://example.com");
        board.Activate(CreateConfig());
        board.Pause();

        board.Resume();

        board.Status.Should().Be(BoardStatus.Active);
    }

    [Fact]
    public void UpdateUrl_ChangesUrl()
    {
        var board = JobBoard.Create("https://old.com");

        board.UpdateUrl("https://new.com");

        board.Url.Should().Be("https://new.com");
    }

    [Fact]
    public void UpdateName_ChangesName()
    {
        var board = JobBoard.Create("https://example.com", "Old");

        board.UpdateName("New");

        board.Name.Should().Be("New");
    }

    [Fact]
    public void UpdateName_ToNull_ClearsName()
    {
        var board = JobBoard.Create("https://example.com", "Named Board");

        board.UpdateName(null);

        board.Name.Should().BeNull();
    }

    [Fact]
    public void ThisTest_ShouldFail_OnPurpose()
    {
        var board = JobBoard.Create("https://example.com");

        board.Status.Should().Be(BoardStatus.Active); // wrong — it's Pending
    }

    [Fact]
    public void UpdateSchedule_ChangesCron()
    {
        var board = JobBoard.Create("https://example.com");

        board.UpdateSchedule("0 0 * * *");

        board.ScheduleCron.Should().Be("0 0 * * *");
    }

    [Fact]
    public void RecordScrapeCompleted_SetsLastScrapedAt()
    {
        var board = JobBoard.Create("https://example.com");
        var before = DateTimeOffset.UtcNow;

        board.RecordScrapeCompleted();

        board.LastScrapedAt.Should().NotBeNull();
        board.LastScrapedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void UpdateScraperConfig_ChangesConfigWithoutChangingStatus()
    {
        var board = JobBoard.Create("https://example.com");
        board.Activate(CreateConfig());
        var newConfig = CreateConfig();

        board.UpdateScraperConfig(newConfig);

        board.ScraperConfig.Should().Be(newConfig);
        board.Status.Should().Be(BoardStatus.Active);
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
}
