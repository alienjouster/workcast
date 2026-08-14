using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class GeneratedLetterTests
{
    [Fact]
    public void Create_SetsAllFieldsAndGeneratesId()
    {
        var appId = Guid.NewGuid();

        var letter = GeneratedLetter.Create(appId, "<p>Dear hiring manager</p>", "claude-sonnet-4-6", 3, false);

        letter.Id.Should().NotBe(Guid.Empty);
        letter.ApplicationId.Should().Be(appId);
        letter.HtmlContent.Should().Be("<p>Dear hiring manager</p>");
        letter.ModelUsed.Should().Be("claude-sonnet-4-6");
        letter.VersionNumber.Should().Be(3);
        letter.IsManualEdit.Should().BeFalse();
        letter.GeneratedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_ManualEdit_SetsIsManualEditTrue()
    {
        var letter = GeneratedLetter.Create(Guid.NewGuid(), "<p>edited</p>", "claude-sonnet-4-6", 2, true);

        letter.IsManualEdit.Should().BeTrue();
    }
}
