using Workcast.Core.Entities;
using Workcast.Core.Models;

namespace Workcast.Core.Tests.Entities;

public class GeneratedResumeTests
{
    [Fact]
    public void Create_SetsAllFieldsAndGeneratesId()
    {
        var appId = Guid.NewGuid();

        var resume = GeneratedResume.Create(appId, "<html>resume</html>", "claude-sonnet-4-6", 1,
            ResumeOptimizationLevel.Medium, false);

        resume.Id.Should().NotBe(Guid.Empty);
        resume.ApplicationId.Should().Be(appId);
        resume.HtmlContent.Should().Be("<html>resume</html>");
        resume.ModelUsed.Should().Be("claude-sonnet-4-6");
        resume.VersionNumber.Should().Be(1);
        resume.OptimizationLevel.Should().Be(ResumeOptimizationLevel.Medium);
        resume.IsManualEdit.Should().BeFalse();
        resume.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_ManualEdit_SetsNullOptimizationLevel()
    {
        var resume = GeneratedResume.Create(Guid.NewGuid(), "<html/>", "claude-sonnet-4-6", 2, null, true);

        resume.IsManualEdit.Should().BeTrue();
        resume.OptimizationLevel.Should().BeNull();
    }
}
