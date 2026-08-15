using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workcast.Api.Tests;

public class ScrapeRunsEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public ScrapeRunsEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Empty_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var runs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        runs.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/runs/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
