using Workcast.Core.Entities;
using Workcast.Core.Enums;

namespace Workcast.Core.Tests.Entities;

public class ScrapeRunTests
{
    [Fact]
    public void Create_SetsStatusEnqueuedAndFields()
    {
        var boardId = Guid.NewGuid();

        var run = ScrapeRun.Create(boardId, TriggerSource.Manual, "hangfire-123");

        run.JobBoardId.Should().Be(boardId);
        run.TriggeredBy.Should().Be(TriggerSource.Manual);
        run.HangfireJobId.Should().Be("hangfire-123");
        run.Status.Should().Be(RunStatus.Enqueued);
        run.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        run.FinishedAt.Should().BeNull();
        run.PagesScraped.Should().Be(0);
        run.AdsFound.Should().Be(0);
        run.AdsNew.Should().Be(0);
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Complete_SetsStatusCompletedAndCounters()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Scheduler, "j1");

        run.Complete(pagesScraped: 5, adsFound: 20, adsNew: 8);

        run.Status.Should().Be(RunStatus.Completed);
        run.FinishedAt.Should().NotBeNull();
        run.PagesScraped.Should().Be(5);
        run.AdsFound.Should().Be(20);
        run.AdsNew.Should().Be(8);
    }

    [Fact]
    public void CompletePartial_SetsStatusPartial()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.CompletePartial(pagesScraped: 3, adsFound: 10, adsNew: 4);

        run.Status.Should().Be(RunStatus.Partial);
        run.FinishedAt.Should().NotBeNull();
        run.PagesScraped.Should().Be(3);
    }

    [Fact]
    public void Fail_SetsStatusFailedAndFinishedAt()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.Fail(pagesScraped: 1, adsFound: 0, adsNew: 0);

        run.Status.Should().Be(RunStatus.Failed);
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_TransitionsToProcessing()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.Start();

        run.Status.Should().Be(RunStatus.Processing);
    }

    [Fact]
    public void SetStatus_SetsArbitraryStatus()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.SetStatus(RunStatus.Scheduled);

        run.Status.Should().Be(RunStatus.Scheduled);
    }

    [Fact]
    public void Delete_SetsStatusDeletedAndFinishedAt()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.Delete();

        run.Status.Should().Be(RunStatus.Deleted);
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddError_AccumulatesErrors()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");

        run.AddError("page1", "timeout");
        run.AddError("page2", "404");

        run.Errors.Should().HaveCount(2);
        run.Errors[0].Page.Should().Be("page1");
        run.Errors[0].Message.Should().Be("timeout");
        run.Errors[1].Page.Should().Be("page2");
        run.Errors[1].Message.Should().Be("404");
    }

    [Fact]
    public void AddError_SetsTimestamp()
    {
        var run = ScrapeRun.Create(Guid.NewGuid(), TriggerSource.Manual, "j1");
        var before = DateTimeOffset.UtcNow;

        run.AddError("page1", "error");

        run.Errors[0].Timestamp.Should().BeOnOrAfter(before);
    }
}
