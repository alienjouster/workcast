using System.Net;
using System.Net.Http.Json;

namespace Workcast.Api.Tests;

public class GoogleDriveEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public GoogleDriveEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Disconnect_Returns204()
    {
        var response = await _client.DeleteAsync("/api/google-drive/connection");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateBasePath_EmptyPath_Returns422()
    {
        var body = new { basePath = "" };
        var response = await _client.PutAsJsonAsync("/api/google-drive/base-path", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdateBasePath_ValidPath_Returns204()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var body = new { basePath = "/my-applications" };
        var response = await client.PutAsJsonAsync("/api/google-drive/base-path", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Callback_NoCode_ReturnsHtmlWithError()
    {
        var response = await _client.GetAsync("/api/google-drive/callback");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("google-drive-error");
    }
}
