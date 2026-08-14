using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class JobAdTests
{
    [Fact]
    public void Create_SetsRequiredFieldsAndActiveTrue()
    {
        var boardId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        var ad = JobAd.Create(boardId, "https://example.com/job/1", runId);

        ad.JobBoardId.Should().Be(boardId);
        ad.ScrapeRunId.Should().Be(runId);
        ad.Url.Should().Be("https://example.com/job/1");
        ad.IsActive.Should().BeTrue();
        ad.IsManual.Should().BeFalse();
        ad.IsPinned.Should().BeFalse();
        ad.IsRead.Should().BeFalse();
        ad.IsTrashed.Should().BeFalse();
        ad.IsScoringPending.Should().BeFalse();
        ad.ScrapedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithoutRunId_LeavesRunIdNull()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.ScrapeRunId.Should().BeNull();
    }

    [Fact]
    public void CreateManual_SetsIsManualTrueAndNoBoard()
    {
        var ad = JobAd.CreateManual("https://example.com/job/1", "Dev", "Acme", "Paris");

        ad.IsManual.Should().BeTrue();
        ad.JobBoardId.Should().BeNull();
        ad.ScrapeRunId.Should().BeNull();
        ad.Title.Should().Be("Dev");
        ad.Company.Should().Be("Acme");
        ad.Location.Should().Be("Paris");
        ad.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ApplyExtraction_SetsAllFieldsAndParsesDate()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.ApplyExtraction("Engineer", "Acme", "London", "50k", "2025-03-15", "EXT-123", "A great job");

        ad.Title.Should().Be("Engineer");
        ad.Company.Should().Be("Acme");
        ad.Location.Should().Be("London");
        ad.SalaryRaw.Should().Be("50k");
        ad.ExternalId.Should().Be("EXT-123");
        ad.Description.Should().Be("A great job");
        ad.PostedAt.Should().NotBeNull();
    }

    [Fact]
    public void ApplyExtraction_WithUnparseableDate_LeavesPostedAtNull()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.ApplyExtraction("Engineer", null, null, null, "not-a-date", null, null);

        ad.PostedAt.Should().BeNull();
    }

    [Fact]
    public void ApplyExtraction_WithNullDate_LeavesPostedAtNull()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.ApplyExtraction("Engineer", null, null, null, null, null, null);

        ad.PostedAt.Should().BeNull();
    }

    [Fact]
    public void MarkInactive_SetsIsActiveFalse()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.MarkInactive();

        ad.IsActive.Should().BeFalse();
    }

    [Fact]
    public void MarkActive_RestoresIsActiveTrue()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.MarkInactive();

        ad.MarkActive();

        ad.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Pin_Unpin_TogglesIsPinned()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.Pin();
        ad.IsPinned.Should().BeTrue();

        ad.Unpin();
        ad.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void MarkRead_MarkUnread_TogglesIsRead()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.MarkRead();
        ad.IsRead.Should().BeTrue();

        ad.MarkUnread();
        ad.IsRead.Should().BeFalse();
    }

    [Fact]
    public void Trash_Restore_TogglesIsTrashed()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");

        ad.Trash();
        ad.IsTrashed.Should().BeTrue();

        ad.Restore();
        ad.IsTrashed.Should().BeFalse();
    }

    [Fact]
    public void SetScoringPending_SetsFlag_ClearsError()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.SetScoringFailed("previous error");

        ad.SetScoringPending();

        ad.IsScoringPending.Should().BeTrue();
        ad.LastScoringError.Should().BeNull();
    }

    [Fact]
    public void ClearScoringPending_ClearsFlagAndError()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.SetScoringPending();

        ad.ClearScoringPending();

        ad.IsScoringPending.Should().BeFalse();
        ad.LastScoringError.Should().BeNull();
    }

    [Fact]
    public void SetScoringFailed_ClearsFlagAndSetsError()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.SetScoringPending();

        ad.SetScoringFailed("timeout");

        ad.IsScoringPending.Should().BeFalse();
        ad.LastScoringError.Should().Be("timeout");
    }

    [Fact]
    public void UpdateDetails_ChangesUrlTitleCompanyLocation()
    {
        var ad = JobAd.CreateManual("https://old.com", "Old Title", "Old Co", "Old Loc");

        ad.UpdateDetails("https://new.com", "New Title", "New Co", "New Loc");

        ad.Url.Should().Be("https://new.com");
        ad.Title.Should().Be("New Title");
        ad.Company.Should().Be("New Co");
        ad.Location.Should().Be("New Loc");
    }
}
