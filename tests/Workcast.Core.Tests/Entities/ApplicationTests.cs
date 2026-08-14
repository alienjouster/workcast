using Workcast.Core.Entities;
using Workcast.Core.Enums;

namespace Workcast.Core.Tests.Entities;

public class ApplicationTests
{
    [Fact]
    public void CreateFromJobAd_WithoutScoring_CopiesAdFieldsAndDefaultsStatus()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        ad.ApplyExtraction("Engineer", "Acme", "London", "50k", "2025-03-15", "EXT-1", "Great job");

        var app = Application.CreateFromJobAd(ad, scoring: null);

        app.JobAdId.Should().Be(ad.Id);
        app.Url.Should().Be(ad.Url);
        app.Title.Should().Be("Engineer");
        app.Company.Should().Be("Acme");
        app.Location.Should().Be("London");
        app.SalaryRaw.Should().Be("50k");
        app.Description.Should().Be("Great job");
        app.ExternalId.Should().Be("EXT-1");
        app.Status.Should().Be(ApplicationStatus.ToApply);
        app.OverallScore.Should().BeNull();
        app.ScoredAt.Should().BeNull();
        app.Requirements.Should().BeEmpty();
        app.StatusHistory.Should().HaveCount(1);
        app.StatusHistory[0].Status.Should().Be(ApplicationStatus.ToApply);
        app.IsTrashed.Should().BeFalse();
    }

    [Fact]
    public void CreateFromJobAd_WithScoring_CopiesScoringFields()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        var scoring = AdScoring.Create(ad.Id, 85.0, "Strong match", "Keep going",
            [new ScoringRequirement { Name = "C#", Category = "match", Score = 100 }]);

        var app = Application.CreateFromJobAd(ad, scoring);

        app.OverallScore.Should().Be(85.0);
        app.Summary.Should().Be("Strong match");
        app.Recommendation.Should().Be("Keep going");
        app.Requirements.Should().HaveCount(1);
        app.ScoredAt.Should().NotBeNull();
    }

    [Fact]
    public void Trash_Restore_TogglesIsTrashed()
    {
        var app = CreateTestApplication();

        app.Trash();
        app.IsTrashed.Should().BeTrue();

        app.Restore();
        app.IsTrashed.Should().BeFalse();
    }

    [Fact]
    public void ClearJobAdReference_SetsJobAdIdNull()
    {
        var app = CreateTestApplication();
        app.JobAdId.Should().NotBeNull();

        app.ClearJobAdReference();

        app.JobAdId.Should().BeNull();
    }

    [Fact]
    public void UpdateJobAdContent_SetsContent()
    {
        var app = CreateTestApplication();

        app.UpdateJobAdContent("Full page text");

        app.JobAdContent.Should().Be("Full page text");
    }

    [Theory]
    [InlineData("Scoring")]
    [InlineData("ResumeGeneration")]
    [InlineData("LetterGeneration")]
    [InlineData("InterviewDrill")]
    public void PendingFailedCycle_WorksForAllJobTypes(string jobType)
    {
        var app = CreateTestApplication();

        // Set pending — should clear error and set flag
        InvokePending(app, jobType);
        GetPendingFlag(app, jobType).Should().BeTrue();
        GetErrorField(app, jobType).Should().BeNull();

        // Set failed — should clear flag and set error
        InvokeFailed(app, jobType, "something broke");
        GetPendingFlag(app, jobType).Should().BeFalse();
        GetErrorField(app, jobType).Should().Be("something broke");

        // Clear pending — should clear flag
        InvokePending(app, jobType);
        InvokeClearPending(app, jobType);
        GetPendingFlag(app, jobType).Should().BeFalse();
    }

    [Fact]
    public void UpdateScoring_OverwritesScoringFields()
    {
        var app = CreateTestApplication();
        var scoredAt = DateTimeOffset.UtcNow;
        var reqs = new List<ScoringRequirement>
        {
            new() { Name = "Python", Category = "gap", Score = 0 },
        };

        app.UpdateScoring(45.0, scoredAt, "Weak match", "Learn Python", reqs);

        app.OverallScore.Should().Be(45.0);
        app.ScoredAt.Should().Be(scoredAt);
        app.Summary.Should().Be("Weak match");
        app.Recommendation.Should().Be("Learn Python");
        app.Requirements.Should().HaveCount(1);
    }

    [Fact]
    public void SetGoogleDriveFolderId_SetsValue()
    {
        var app = CreateTestApplication();

        app.SetGoogleDriveFolderId("folder-xyz");

        app.GoogleDriveFolderId.Should().Be("folder-xyz");
    }

    // ── Status transition tests ──────────────────────────────────────────

    [Fact]
    public void UpdateStatus_ForwardTransition_AddsHistoryEntry()
    {
        var app = CreateTestApplication();

        app.UpdateStatus(ApplicationStatus.Applied);

        app.Status.Should().Be(ApplicationStatus.Applied);
        app.StatusHistory.Should().HaveCount(2);
        app.StatusHistory[1].Status.Should().Be(ApplicationStatus.Applied);
    }

    [Fact]
    public void UpdateStatus_BackwardTransition_TrimsFutureEntries()
    {
        var app = CreateTestApplication();
        app.UpdateStatus(ApplicationStatus.Applied);
        app.UpdateStatus(ApplicationStatus.Interviewing);

        app.UpdateStatus(ApplicationStatus.Applied);

        app.Status.Should().Be(ApplicationStatus.Applied);
        app.StatusHistory.Should().HaveCount(2);
        app.StatusHistory.Should().NotContain(e => e.Status == ApplicationStatus.Interviewing);
    }

    [Fact]
    public void UpdateStatus_ClosedStatuses_AreMutuallyExclusive()
    {
        var app = CreateTestApplication();
        app.UpdateStatus(ApplicationStatus.Applied);
        app.UpdateStatus(ApplicationStatus.ClosedNoAnswer);

        app.UpdateStatus(ApplicationStatus.ClosedRejected);

        app.Status.Should().Be(ApplicationStatus.ClosedRejected);
        app.StatusHistory.Should().NotContain(e => e.Status == ApplicationStatus.ClosedNoAnswer);
        app.StatusHistory.Should().Contain(e => e.Status == ApplicationStatus.ClosedRejected);
    }

    [Fact]
    public void UpdateStatus_ClosedToNonClosed_PreservesNonClosedHistory()
    {
        var app = CreateTestApplication();
        app.UpdateStatus(ApplicationStatus.Applied);
        app.UpdateStatus(ApplicationStatus.ClosedNoAnswer);

        app.UpdateStatus(ApplicationStatus.Interviewing);

        app.Status.Should().Be(ApplicationStatus.Interviewing);
        app.StatusHistory.Should().Contain(e => e.Status == ApplicationStatus.ToApply);
        app.StatusHistory.Should().Contain(e => e.Status == ApplicationStatus.Applied);
        app.StatusHistory.Should().Contain(e => e.Status == ApplicationStatus.Interviewing);
        app.StatusHistory.Should().NotContain(e => e.Status == ApplicationStatus.ClosedNoAnswer);
    }

    [Fact]
    public void UpdateStatus_WithExplicitDate_UsesProvidedDate()
    {
        var app = CreateTestApplication();
        var customDate = new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

        app.UpdateStatus(ApplicationStatus.Applied, customDate);

        app.StatusHistory.Last().AchievedAt.Should().Be(customDate);
    }

    [Fact]
    public void UpdateStatus_SameStatusWithExplicitDate_OverwritesDate()
    {
        var app = CreateTestApplication();
        var newDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        app.UpdateStatus(ApplicationStatus.ToApply, newDate);

        app.StatusHistory.Should().HaveCount(1);
        app.StatusHistory[0].AchievedAt.Should().Be(newDate);
    }

    [Fact]
    public void UpdateStatusDate_ExistingStatus_UpdatesDate()
    {
        var app = CreateTestApplication();
        app.UpdateStatus(ApplicationStatus.Applied);
        var newDate = new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero);

        app.UpdateStatusDate(ApplicationStatus.Applied, newDate);

        app.StatusHistory.First(e => e.Status == ApplicationStatus.Applied)
            .AchievedAt.Should().Be(newDate);
    }

    [Fact]
    public void UpdateStatusDate_NonExistentStatus_DoesNothing()
    {
        var app = CreateTestApplication();
        var originalCount = app.StatusHistory.Count;

        app.UpdateStatusDate(ApplicationStatus.Interviewing, DateTimeOffset.UtcNow);

        app.StatusHistory.Should().HaveCount(originalCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static Application CreateTestApplication()
    {
        var ad = JobAd.Create(Guid.NewGuid(), "https://example.com/job/1");
        return Application.CreateFromJobAd(ad, scoring: null);
    }

    private static void InvokePending(Application app, string jobType)
    {
        switch (jobType)
        {
            case "Scoring": app.SetScoringPending(); break;
            case "ResumeGeneration": app.SetResumeGenerationPending(); break;
            case "LetterGeneration": app.SetLetterGenerationPending(); break;
            case "InterviewDrill": app.SetInterviewDrillPending(); break;
        }
    }

    private static void InvokeClearPending(Application app, string jobType)
    {
        switch (jobType)
        {
            case "Scoring": app.ClearScoringPending(); break;
            case "ResumeGeneration": app.ClearResumeGenerationPending(); break;
            case "LetterGeneration": app.ClearLetterGenerationPending(); break;
            case "InterviewDrill": app.ClearInterviewDrillPending(); break;
        }
    }

    private static void InvokeFailed(Application app, string jobType, string error)
    {
        switch (jobType)
        {
            case "Scoring": app.SetScoringFailed(error); break;
            case "ResumeGeneration": app.SetResumeGenerationFailed(error); break;
            case "LetterGeneration": app.SetLetterGenerationFailed(error); break;
            case "InterviewDrill": app.SetInterviewDrillFailed(error); break;
        }
    }

    private static bool GetPendingFlag(Application app, string jobType) => jobType switch
    {
        "Scoring" => app.IsScoringPending,
        "ResumeGeneration" => app.IsResumeGenerationPending,
        "LetterGeneration" => app.IsLetterGenerationPending,
        "InterviewDrill" => app.IsInterviewDrillPending,
        _ => throw new ArgumentException(jobType),
    };

    private static string? GetErrorField(Application app, string jobType) => jobType switch
    {
        "Scoring" => app.LastScoringError,
        "ResumeGeneration" => app.LastResumeGenerationError,
        "LetterGeneration" => app.LastLetterGenerationError,
        "InterviewDrill" => app.LastInterviewDrillError,
        _ => throw new ArgumentException(jobType),
    };
}
