using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.AI;

/// <summary>
/// Fetches the list of available Claude models from Anthropic's /v1/models endpoint
/// and caches the result in-process for one hour. Falls back to a static list when
/// the API is unreachable or returns an error.
/// </summary>
public sealed class AnthropicModelsService : IAnthropicModelsService
{
    private const string CacheKey = "anthropic_models";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly IReadOnlyList<AvailableModelDto> Fallback =
    [
        new("claude-haiku-4-5-20251001", "Claude Haiku 4.5"),
        new("claude-sonnet-4-5",         "Claude Sonnet 4.5"),
        new("claude-sonnet-4-6",         "Claude Sonnet 4.6"),
        new("claude-opus-4-6",           "Claude Opus 4.6"),
        new("claude-opus-4-8",           "Claude Opus 4.8"),
    ];

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnthropicModelsService> _logger;

    public AnthropicModelsService(HttpClient http, IMemoryCache cache, ILogger<AnthropicModelsService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableModelDto>> GetModelsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<AvailableModelDto>? cached))
            return cached!;

        try
        {
            using var response = await _http.GetAsync(
                "https://api.anthropic.com/v1/models?limit=100", ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var models = new List<AvailableModelDto>();
            foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                if (!id.StartsWith("claude-", StringComparison.OrdinalIgnoreCase))
                    continue;

                var displayName = item.TryGetProperty("display_name", out var dn)
                    ? dn.GetString() ?? id
                    : id;

                models.Add(new AvailableModelDto(id, displayName));
            }

            IReadOnlyList<AvailableModelDto> result = models.Count > 0 ? models : Fallback;
            _cache.Set(CacheKey, result, CacheDuration);
            _logger.LogInformation("Anthropic models fetched: {Models}",
                string.Join(", ", result.Select(m => m.Id)));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch models from Anthropic API; using static fallback list: {Models}",
                string.Join(", ", Fallback.Select(m => m.Id)));
            return Fallback;
        }
    }
}
