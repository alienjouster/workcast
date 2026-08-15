using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workcast.Api.Tests;

public class SettingsEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public SettingsEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSettings_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchSettings_InvalidModel_Returns422()
    {
        var body = new
        {
            boardAnalyzerModel = "gpt-4",
            scoringModel = "claude-sonnet-4-5",
            resumeGenerationModel = "claude-sonnet-4-5",
            letterGenerationModel = "claude-sonnet-4-5",
            interviewTrainerModel = "claude-sonnet-4-5",
            interviewAnswerEvaluationModel = "claude-sonnet-4-5",
            boardAnalyzerMaxTokens = 4096,
            scoringMaxTokens = 4096,
            resumeGenerationMaxTokens = 4096,
            letterGenerationMaxTokens = 4096,
            interviewTrainerMaxTokens = 4096,
            interviewAnswerEvaluationMaxTokens = 4096,
        };

        var response = await _client.PatchAsJsonAsync("/api/settings", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DeleteResume_WhenNoneExists_Returns200WithSettings()
    {
        var response = await _client.DeleteAsync("/api/settings/resume");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetResumeContent_WhenNoneExists_Returns404()
    {
        var response = await _client.GetAsync("/api/settings/resume/content");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteResumeTemplate_WhenNoneExists_Returns200WithSettings()
    {
        var response = await _client.DeleteAsync("/api/settings/resume-template");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadResume_ValidPdf_Returns200()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var body = new
        {
            fileName = "resume.pdf",
            contentBase64 = Convert.ToBase64String(pdfBytes),
            contentType = "application/pdf",
        };

        var response = await client.PutAsJsonAsync("/api/settings/resume", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadResume_InvalidContentType_Returns422()
    {
        var body = new
        {
            fileName = "resume.docx",
            contentBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            contentType = "application/msword",
        };

        var response = await _client.PutAsJsonAsync("/api/settings/resume", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
