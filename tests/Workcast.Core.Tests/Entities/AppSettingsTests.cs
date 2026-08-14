using Workcast.Core.Entities;

namespace Workcast.Core.Tests.Entities;

public class AppSettingsTests
{
    [Fact]
    public void CreateDefault_SetsIdToOneWithDefaults()
    {
        var settings = AppSettings.CreateDefault();

        settings.Id.Should().Be(1);
        settings.BoardAnalyzerModel.Should().Be("claude-sonnet-4-5");
        settings.ScoringModel.Should().Be("claude-haiku-4-5-20251001");
        settings.ResumeGenerationModel.Should().Be("claude-sonnet-4-6");
        settings.LetterGenerationModel.Should().Be("claude-sonnet-4-6");
        settings.InterviewTrainerModel.Should().Be("claude-sonnet-4-5");
        settings.InterviewAnswerEvaluationModel.Should().Be("claude-sonnet-4-5");
        settings.BoardAnalyzerMaxTokens.Should().Be(4096);
        settings.ScoringMaxTokens.Should().Be(4096);
        settings.ResumeGenerationMaxTokens.Should().Be(16384);
        settings.LetterGenerationMaxTokens.Should().Be(2048);
        settings.InterviewTrainerMaxTokens.Should().Be(4096);
        settings.InterviewAnswerEvaluationMaxTokens.Should().Be(1024);
        settings.HasResume.Should().BeFalse();
        settings.HasResumeTemplate.Should().BeFalse();
        settings.IsGoogleDriveConnected.Should().BeFalse();
        settings.GoogleDriveBasePath.Should().Be("jobs");
    }

    [Fact]
    public void SetResume_StoresFileAndSetsHasResume()
    {
        var settings = AppSettings.CreateDefault();
        var content = new byte[] { 1, 2, 3 };

        settings.SetResume("resume.pdf", content, "application/pdf");

        settings.HasResume.Should().BeTrue();
        settings.ResumeFileName.Should().Be("resume.pdf");
        settings.ResumeContent.Should().BeEquivalentTo(content);
        settings.ResumeContentType.Should().Be("application/pdf");
        settings.ResumeUploadedAt.Should().NotBeNull();
    }

    [Fact]
    public void ClearResume_RemovesAllResumeFields()
    {
        var settings = AppSettings.CreateDefault();
        settings.SetResume("resume.pdf", [1, 2], "application/pdf");

        settings.ClearResume();

        settings.HasResume.Should().BeFalse();
        settings.ResumeFileName.Should().BeNull();
        settings.ResumeContent.Should().BeNull();
        settings.ResumeContentType.Should().BeNull();
        settings.ResumeUploadedAt.Should().BeNull();
    }

    [Fact]
    public void SetResumeTemplate_StoresTemplateAndSetsHasTemplate()
    {
        var settings = AppSettings.CreateDefault();

        settings.SetResumeTemplate("template.html", "<html>{{content}}</html>");

        settings.HasResumeTemplate.Should().BeTrue();
        settings.ResumeTemplateFileName.Should().Be("template.html");
        settings.ResumeTemplateContent.Should().Be("<html>{{content}}</html>");
        settings.ResumeTemplateUploadedAt.Should().NotBeNull();
    }

    [Fact]
    public void ClearResumeTemplate_RemovesAllTemplateFields()
    {
        var settings = AppSettings.CreateDefault();
        settings.SetResumeTemplate("template.html", "<html/>");

        settings.ClearResumeTemplate();

        settings.HasResumeTemplate.Should().BeFalse();
        settings.ResumeTemplateFileName.Should().BeNull();
        settings.ResumeTemplateContent.Should().BeNull();
        settings.ResumeTemplateUploadedAt.Should().BeNull();
    }

    [Fact]
    public void SetGoogleDriveRefreshToken_SetsTokenAndConnects()
    {
        var settings = AppSettings.CreateDefault();

        settings.SetGoogleDriveRefreshToken("token-123");

        settings.IsGoogleDriveConnected.Should().BeTrue();
        settings.GoogleDriveRefreshToken.Should().Be("token-123");
    }

    [Fact]
    public void ClearGoogleDriveRefreshToken_DisconnectsAndClearsFolderId()
    {
        var settings = AppSettings.CreateDefault();
        settings.SetGoogleDriveRefreshToken("token-123");
        settings.SetGoogleDriveBaseFolderId("folder-abc");

        settings.ClearGoogleDriveRefreshToken();

        settings.IsGoogleDriveConnected.Should().BeFalse();
        settings.GoogleDriveRefreshToken.Should().BeNull();
        settings.GoogleDriveBaseFolderId.Should().BeNull();
    }

    [Fact]
    public void SetGoogleDriveBasePath_UpdatesPathAndInvalidatesFolderId()
    {
        var settings = AppSettings.CreateDefault();
        settings.SetGoogleDriveBaseFolderId("folder-abc");

        settings.SetGoogleDriveBasePath("applications");

        settings.GoogleDriveBasePath.Should().Be("applications");
        settings.GoogleDriveBaseFolderId.Should().BeNull();
    }

    [Fact]
    public void SetModels_UpdatesEachModelIndependently()
    {
        var settings = AppSettings.CreateDefault();

        settings.SetBoardAnalyzerModel("claude-opus-4-6");
        settings.SetScoringModel("claude-haiku-4-5");
        settings.SetResumeGenerationModel("claude-opus-4-6");
        settings.SetLetterGenerationModel("claude-opus-4-6");
        settings.SetInterviewTrainerModel("claude-opus-4-6");
        settings.SetInterviewAnswerEvaluationModel("claude-opus-4-6");

        settings.BoardAnalyzerModel.Should().Be("claude-opus-4-6");
        settings.ScoringModel.Should().Be("claude-haiku-4-5");
        settings.ResumeGenerationModel.Should().Be("claude-opus-4-6");
        settings.LetterGenerationModel.Should().Be("claude-opus-4-6");
        settings.InterviewTrainerModel.Should().Be("claude-opus-4-6");
        settings.InterviewAnswerEvaluationModel.Should().Be("claude-opus-4-6");
    }

    [Fact]
    public void SetMaxTokens_UpdatesEachValueIndependently()
    {
        var settings = AppSettings.CreateDefault();

        settings.SetBoardAnalyzerMaxTokens(8192);
        settings.SetScoringMaxTokens(2048);
        settings.SetResumeGenerationMaxTokens(32768);
        settings.SetLetterGenerationMaxTokens(4096);
        settings.SetInterviewTrainerMaxTokens(8192);
        settings.SetInterviewAnswerEvaluationMaxTokens(2048);

        settings.BoardAnalyzerMaxTokens.Should().Be(8192);
        settings.ScoringMaxTokens.Should().Be(2048);
        settings.ResumeGenerationMaxTokens.Should().Be(32768);
        settings.LetterGenerationMaxTokens.Should().Be(4096);
        settings.InterviewTrainerMaxTokens.Should().Be(8192);
        settings.InterviewAnswerEvaluationMaxTokens.Should().Be(2048);
    }
}
