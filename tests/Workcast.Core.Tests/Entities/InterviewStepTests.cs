using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class InterviewStepTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var appId = Guid.NewGuid();
        var interviewers = new List<InterviewStepInterviewer>
        {
            new() { Name = "Alice", JobFunction = "Engineering Manager" },
        };

        var step = InterviewStep.Create(appId, 1, new DateOnly(2025, 8, 20), new TimeOnly(14, 0),
            60, "CEST", false, "https://zoom.us/123", interviewers, "Technical round");

        step.ApplicationId.Should().Be(appId);
        step.StepNumber.Should().Be(1);
        step.Date.Should().Be(new DateOnly(2025, 8, 20));
        step.Time.Should().Be(new TimeOnly(14, 0));
        step.DurationMinutes.Should().Be(60);
        step.Timezone.Should().Be("CEST");
        step.IsOnSite.Should().BeFalse();
        step.RemoteCallLink.Should().Be("https://zoom.us/123");
        step.Interviewers.Should().HaveCount(1);
        step.Notes.Should().Be("Technical round");
        step.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithNullOptionalFields_Succeeds()
    {
        var step = InterviewStep.Create(Guid.NewGuid(), 1, null, null, null, "UTC", true, null, [], null);

        step.Date.Should().BeNull();
        step.Time.Should().BeNull();
        step.DurationMinutes.Should().BeNull();
        step.IsOnSite.Should().BeTrue();
        step.RemoteCallLink.Should().BeNull();
        step.Notes.Should().BeNull();
    }

    [Fact]
    public void Update_OverwritesAllMutableFields()
    {
        var step = InterviewStep.Create(Guid.NewGuid(), 1, null, null, null, "UTC", false, null, [], null);

        step.Update(new DateOnly(2025, 9, 1), new TimeOnly(10, 30), 45, "CET", true, null,
            [new InterviewStepInterviewer { Name = "Bob", JobFunction = "CTO" }], "Final round");

        step.Date.Should().Be(new DateOnly(2025, 9, 1));
        step.Time.Should().Be(new TimeOnly(10, 30));
        step.DurationMinutes.Should().Be(45);
        step.Timezone.Should().Be("CET");
        step.IsOnSite.Should().BeTrue();
        step.Interviewers.Should().HaveCount(1);
        step.Notes.Should().Be("Final round");
    }

    [Fact]
    public void Renumber_ChangesStepNumber()
    {
        var step = InterviewStep.Create(Guid.NewGuid(), 3, null, null, null, "UTC", false, null, [], null);

        step.Renumber(1);

        step.StepNumber.Should().Be(1);
    }
}
