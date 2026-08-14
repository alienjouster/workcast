using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class AdScoringTests
{
    [Fact]
    public void Create_SetsAllFieldsCorrectly()
    {
        var adId = Guid.NewGuid();
        var requirements = new List<ScoringRequirement>
        {
            new() { Name = "React", Category = "match", Score = 100, IsOptional = false },
            new() { Name = "Go", Category = "gap", Score = 0, IsOptional = true, Notes = "Not mentioned" },
        };

        var scoring = AdScoring.Create(adId, 72.5, "Good match", "Learn Go", requirements);

        scoring.JobAdId.Should().Be(adId);
        scoring.OverallScore.Should().Be(72.5);
        scoring.Summary.Should().Be("Good match");
        scoring.Recommendation.Should().Be("Learn Go");
        scoring.Requirements.Should().HaveCount(2);
        scoring.ScoredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyRequirements_Succeeds()
    {
        var scoring = AdScoring.Create(Guid.NewGuid(), 0, "", "", []);

        scoring.Requirements.Should().BeEmpty();
    }
}
