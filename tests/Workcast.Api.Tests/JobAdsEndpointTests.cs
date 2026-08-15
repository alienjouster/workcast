using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Workcast.Api.Tests;

public class JobAdsEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public JobAdsEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Empty_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/job-ads");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/job-ads/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/job-ads/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateManual_ReturnsCreated()
    {
        var body = new
        {
            url = "https://example.com/job/123",
            title = "Software Engineer",
            company = "Acme Corp",
            location = "Zurich",
        };

        var response = await _client.PostAsJsonAsync("/api/job-ads", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("title").GetString().Should().Be("Software Engineer");
        json.GetProperty("company").GetString().Should().Be("Acme Corp");
    }

    [Fact]
    public async Task Pin_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/job-ads/{Guid.NewGuid()}/pin", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unpin_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/job-ads/{Guid.NewGuid()}/unpin", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Read_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/job-ads/{Guid.NewGuid()}/read", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Trash_NonExistent_Returns404()
    {
        var response = await _client.PatchAsync($"/api/job-ads/{Guid.NewGuid()}/trash", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateManual_ThenGet_ReturnsAd()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var body = new
        {
            url = "https://example.com/job/456",
            title = "Data Engineer",
            company = "DataCo",
            location = "Geneva",
        };
        var createResponse = await client.PostAsJsonAsync("/api/job-ads", body);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/job-ads/{id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("title").GetString().Should().Be("Data Engineer");
    }

    [Fact]
    public async Task CreateManual_ThenPinUnpin_WorksCorrectly()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var body = new { url = "https://example.com/job/789", title = "QA", company = "Co", location = "Bern" };
        var createResponse = await client.PostAsJsonAsync("/api/job-ads", body);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var pinResponse = await client.PatchAsync($"/api/job-ads/{id}/pin", null);
        pinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pinned = await pinResponse.Content.ReadFromJsonAsync<JsonElement>();
        pinned.GetProperty("isPinned").GetBoolean().Should().BeTrue();

        var unpinResponse = await client.PatchAsync($"/api/job-ads/{id}/unpin", null);
        unpinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unpinned = await unpinResponse.Content.ReadFromJsonAsync<JsonElement>();
        unpinned.GetProperty("isPinned").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreateManual_ThenTrashRestore_WorksCorrectly()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var body = new { url = "https://example.com/job/trash", title = "Ops", company = "Co", location = "Basel" };
        var createResponse = await client.PostAsJsonAsync("/api/job-ads", body);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var trashResponse = await client.PatchAsync($"/api/job-ads/{id}/trash", null);
        trashResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var restoreResponse = await client.PatchAsync($"/api/job-ads/{id}/restore", null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateManual_ThenDelete_Returns204()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        var body = new { url = "https://example.com/job/del", title = "Del", company = "Co", location = "Lux" };
        var createResponse = await client.PostAsJsonAsync("/api/job-ads", body);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"/api/job-ads/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/job-ads/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DistinctTitles_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/job-ads/distinct-titles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DistinctLocations_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/job-ads/distinct-locations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DistinctCompanies_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/job-ads/distinct-companies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var body = new { url = "https://example.com/job/x", title = "Updated Title" };
        var response = await _client.PatchAsJsonAsync($"/api/job-ads/{Guid.NewGuid()}", body);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
