using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workcast.Api.Tests;

public class StatusEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public StatusEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetStatus_ReturnsOkWithExpectedShape()
    {
        var response = await _client.GetAsync("/api/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("isProcessing").ValueKind.Should().Be(JsonValueKind.False);
        json.GetProperty("unreadCount").GetInt32().Should().Be(0);
    }
}
