using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workcast.Api.Tests;

public class ApplicationsEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public ApplicationsEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Empty_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/applications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/applications/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stats_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/applications/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Trash_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/applications/{Guid.NewGuid()}/trash", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/applications/{Guid.NewGuid()}/restore", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DistinctTitles_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/applications/distinct-titles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DistinctLocations_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/applications/distinct-locations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DistinctCompanies_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/applications/distinct-companies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
