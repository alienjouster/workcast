using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Workcast.Core.Entities;
using Workcast.Infrastructure.Persistence;

namespace Workcast.Api.Tests;

public class JobBoardsEndpointTests : IClassFixture<WorkcastApiFactory>
{
    private readonly HttpClient _client;

    public JobBoardsEndpointTests(WorkcastApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_Empty_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync("/api/job-boards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var boards = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        boards.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/job-boards/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/job-boards/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refresh_NonExistent_Returns404()
    {
        var response = await _client.PostAsync($"/api/job-boards/{Guid.NewGuid()}/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reanalyze_NonExistent_Returns404()
    {
        var response = await _client.PostAsync($"/api/job-boards/{Guid.NewGuid()}/reanalyze", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Export_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/job-boards/{Guid.NewGuid()}/export");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListRuns_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/job-boards/{Guid.NewGuid()}/runs");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateScraperConfig_NonExistent_ReturnsBadRequestOrNotFound()
    {
        var body = new
        {
            paginationType = "none",
            jobCardSelector = ".job",
            fieldSelectors = new { title = new { selector = "h2" } },
            requiresJs = false,
            suggestedDelayMs = 1000,
            confidenceScore = 0.9,
            generatedAt = DateTimeOffset.UtcNow,
        };
        var response = await _client.PutAsJsonAsync($"/api/job-boards/{Guid.NewGuid()}/scraper-config", body);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_NonExistent_Returns404()
    {
        var body = new { name = "Updated" };
        var response = await _client.PatchAsJsonAsync($"/api/job-boards/{Guid.NewGuid()}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_InvalidStatus_Returns422()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        Guid boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var board = JobBoard.Create("https://example.com/jobs");
            db.JobBoards.Add(board);
            await db.SaveChangesAsync();
            boardId = board.Id;
        }

        var body = new { status = "invalid_status" };
        var response = await client.PatchAsJsonAsync($"/api/job-boards/{boardId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task CreateBoard_ThenList_ReturnsBoardInList()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        Guid boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var board = JobBoard.Create("https://example.com/test-jobs", "Test Board");
            db.JobBoards.Add(board);
            await db.SaveChangesAsync();
            boardId = board.Id;
        }

        var response = await client.GetAsync("/api/job-boards");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var boards = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        boards.Should().HaveCount(1);
        boards![0].GetProperty("id").GetGuid().Should().Be(boardId);
    }

    [Fact]
    public async Task CreateBoard_ThenGet_ReturnsBoard()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        Guid boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var board = JobBoard.Create("https://example.com/get-test", "Get Test");
            db.JobBoards.Add(board);
            await db.SaveChangesAsync();
            boardId = board.Id;
        }

        var response = await client.GetAsync($"/api/job-boards/{boardId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("Get Test");
    }

    [Fact]
    public async Task CreateBoard_ThenDelete_Returns204()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        Guid boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var board = JobBoard.Create("https://example.com/del-test");
            db.JobBoards.Add(board);
            await db.SaveChangesAsync();
            boardId = board.Id;
        }

        var deleteResponse = await client.DeleteAsync($"/api/job-boards/{boardId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/job-boards/{boardId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_UpdateName_Returns200()
    {
        var factory = new WorkcastApiFactory();
        var client = factory.CreateClient();

        Guid boardId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var board = JobBoard.Create("https://example.com/rename", "Old Name");
            db.JobBoards.Add(board);
            await db.SaveChangesAsync();
            boardId = board.Id;
        }

        var body = new { name = "New Name" };
        var response = await client.PatchAsJsonAsync($"/api/job-boards/{boardId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("New Name");
    }
}
