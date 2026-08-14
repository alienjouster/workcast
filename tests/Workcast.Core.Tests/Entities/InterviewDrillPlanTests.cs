using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class InterviewDrillPlanTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var appId = Guid.NewGuid();
        var questions = new List<InterviewQuestion>
        {
            new() { OrderIndex = 1, Text = "Tell me about yourself", Category = "warm_up" },
            new() { OrderIndex = 2, Text = "Explain dependency injection", Category = "medium", RequirementName = "C#" },
        };

        var plan = InterviewDrillPlan.Create(appId, "claude-sonnet-4-5", questions);

        plan.ApplicationId.Should().Be(appId);
        plan.ModelUsed.Should().Be("claude-sonnet-4-5");
        plan.Questions.Should().HaveCount(2);
        plan.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyQuestions_Succeeds()
    {
        var plan = InterviewDrillPlan.Create(Guid.NewGuid(), "model", []);

        plan.Questions.Should().BeEmpty();
    }
}
