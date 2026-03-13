using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.AI.Options;

namespace Workcast.Infrastructure.AI;

/// <summary>
/// Anthropic Claude implementation of <see cref="IAiProvider"/>.
/// Uses the Claude Tool Use API to guarantee structured, machine-readable responses.
/// The board analysis operation forces a specific tool call so Claude cannot return free-form text.
/// Job ad field extraction is fully deterministic via the CSS selectors returned by board analysis —
/// no per-ad AI call is made.
/// See TECHSPEC sections 4.2–4.7 for the full specification.
/// </summary>
public sealed class ClaudeAiProvider : IAiProvider
{
    private const string ApiEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxTokens = 1024;
    private const int MaxRetries = 3;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly ILogger<ClaudeAiProvider> _logger;

    /// <summary>
    /// Initializes a new <see cref="ClaudeAiProvider"/>.
    /// </summary>
    public ClaudeAiProvider(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BoardAnalysisResult> AnalyzeBoardAsync(
        string html,
        string url,
        CancellationToken ct = default)
    {
        var tool = BuildBoardAnalysisTool();
        var prompt = $"""
            You are analyzing a job board website to generate a scraping configuration.

            URL: {url}

            Examine the HTML below and call the save_board_config tool with a complete configuration
            describing how to scrape job listings from this board. Focus on:
            - The CSS selector that identifies each job card element on the listing page (job_card_selector)
            - Per-field CSS selectors relative to each job card (field_selectors):
                detail_url: selector whose href attribute is the job ad URL
                title: selector for the job title text
                company: selector for the company name text
                location: selector for the location text
                salary_raw: selector for raw salary text (null if absent)
                posted_at: selector for the posting date (null if absent)
                description_snippet: selector for a short description preview (null if absent)
                external_id: selector or attribute expression for a board-specific job ID (null if absent)
            - How pagination works
            - Whether JavaScript is required
            - A safe request delay

            HTML:
            {html}
            """;

        var input = await CallWithRetryAsync(prompt, tool, "save_board_config", ct).ConfigureAwait(false);
        return DeserializeBoardAnalysisResult(input);
    }

    private async Task<JsonObject> CallWithRetryAsync(
        string prompt,
        ClaudeTool tool,
        string toolName,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= MaxRetries - 1; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning(
                    "Anthropic API call failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                    attempt, MaxRetries, RetryDelays[attempt - 1].TotalSeconds);

                await Task.Delay(RetryDelays[attempt - 1], ct).ConfigureAwait(false);
            }

            try
            {
                var request = new ClaudeRequest
                {
                    Model = _options.Model,
                    MaxTokens = MaxTokens,
                    Temperature = 0,
                    Tools = [tool],
                    ToolChoice = new ClaudeToolChoice { Type = "tool", Name = toolName },
                    Messages =
                    [
                        new ClaudeMessage { Role = "user", Content = prompt },
                    ],
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                var response = await _httpClient
                    .PostAsJsonAsync(ApiEndpoint, request, JsonOptions, cts.Token)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var claudeResponse = await response.Content
                    .ReadFromJsonAsync<ClaudeResponse>(JsonOptions, cts.Token)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Claude API returned an empty response.");

                var toolUseBlock = claudeResponse.Content
                    .FirstOrDefault(c => c.Type == "tool_use" && c.Name == toolName)
                    ?? throw new InvalidOperationException(
                        $"Claude response did not contain a '{toolName}' tool_use block. " +
                        $"Stop reason: {claudeResponse.StopReason}");

                return toolUseBlock.Input
                    ?? throw new InvalidOperationException("Claude tool_use block has no input.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Propagate genuine cancellation — do not retry.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Anthropic API call attempt {Attempt} failed for tool {Tool}.", attempt + 1, toolName);
                lastException = ex;
            }
        }

        throw new InvalidOperationException(
            $"Anthropic API call failed after {MaxRetries} attempts.", lastException);
    }

    private static BoardAnalysisResult DeserializeBoardAnalysisResult(JsonObject input)
    {
        var paginationRaw = input["pagination_type"]?.GetValue<string>() ?? "none";
        var paginationType = paginationRaw switch
        {
            "url_param" => PaginationType.UrlParam,
            "next_button" => PaginationType.NextButton,
            "infinite_scroll" => PaginationType.InfiniteScroll,
            _ => PaginationType.None,
        };

        var fieldSelectorsNode = input["field_selectors"] as JsonObject;

        return new BoardAnalysisResult
        {
            PaginationType = paginationType,
            JobCardSelector = input["job_card_selector"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Claude did not return job_card_selector."),
            FieldSelectors = new FieldSelectorMap
            {
                DetailUrl = fieldSelectorsNode?["detail_url"]?.GetValue<string?>(),
                Title = fieldSelectorsNode?["title"]?.GetValue<string?>(),
                Company = fieldSelectorsNode?["company"]?.GetValue<string?>(),
                Location = fieldSelectorsNode?["location"]?.GetValue<string?>(),
                SalaryRaw = fieldSelectorsNode?["salary_raw"]?.GetValue<string?>(),
                PostedAt = fieldSelectorsNode?["posted_at"]?.GetValue<string?>(),
                DescriptionSnippet = fieldSelectorsNode?["description_snippet"]?.GetValue<string?>(),
                ExternalId = fieldSelectorsNode?["external_id"]?.GetValue<string?>(),
            },
            NextPageSelector = input["next_page_selector"]?.GetValue<string?>(),
            UrlParamName = input["url_param_name"]?.GetValue<string?>(),
            UrlParamIsOffset = input["url_param_is_offset"]?.GetValue<bool>() ?? false,
            MaxPages = input["max_pages"]?.GetValue<int?>(),
            RequiresJs = input["requires_js"]?.GetValue<bool>() ?? true,
            SuggestedDelayMs = input["suggested_delay_ms"]?.GetValue<int>() ?? 500,
            ConfidenceScore = (float)(input["confidence_score"]?.GetValue<double>() ?? 0.0),
            AnalyzerNotes = input["analyzer_notes"]?.GetValue<string?>(),
        };
    }

    private static ClaudeTool BuildBoardAnalysisTool()
    {
        return new ClaudeTool
        {
            Name = "save_board_config",
            Description = "Save the scraping configuration for a job board after analysis.",
            InputSchema = new
            {
                type = "object",
                required = new[] { "pagination_type", "job_card_selector", "field_selectors", "requires_js", "suggested_delay_ms", "confidence_score" },
                properties = new
                {
                    pagination_type = new { type = "string", @enum = new[] { "url_param", "next_button", "infinite_scroll", "none" } },
                    job_card_selector = new { type = "string", description = "CSS selector matching each job card element on the listing page." },
                    field_selectors = new
                    {
                        type = "object",
                        description = "CSS selectors for individual fields, evaluated relative to each job card element.",
                        properties = new
                        {
                            detail_url          = new { type = new[] { "string", "null" }, description = "Selector whose href is the job detail URL." },
                            title               = new { type = new[] { "string", "null" }, description = "Selector for the job title text." },
                            company             = new { type = new[] { "string", "null" } },
                            location            = new { type = new[] { "string", "null" } },
                            salary_raw          = new { type = new[] { "string", "null" } },
                            posted_at           = new { type = new[] { "string", "null" } },
                            description_snippet = new { type = new[] { "string", "null" } },
                            external_id         = new { type = new[] { "string", "null" } },
                        },
                    },
                    next_page_selector = new { type = new[] { "string", "null" } },
                    url_param_name = new { type = new[] { "string", "null" } },
                    url_param_is_offset = new { type = "boolean" },
                    max_pages = new { type = new[] { "integer", "null" }, minimum = 1 },
                    requires_js = new { type = "boolean" },
                    suggested_delay_ms = new { type = "integer", minimum = 0, maximum = 10000 },
                    confidence_score = new { type = "number", minimum = 0, maximum = 1 },
                    analyzer_notes = new { type = new[] { "string", "null" } },
                },
            },
        };
    }

    // ── Internal request/response models ──────────────────────────────────────

    private sealed class ClaudeRequest
    {
        public string Model { get; init; } = string.Empty;
        public int MaxTokens { get; init; }
        public int Temperature { get; init; }
        public IList<ClaudeTool> Tools { get; init; } = [];
        public ClaudeToolChoice? ToolChoice { get; init; }
        public IList<ClaudeMessage> Messages { get; init; } = [];
    }

    private sealed class ClaudeTool
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public object InputSchema { get; init; } = new();
    }

    private sealed class ClaudeToolChoice
    {
        public string Type { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ClaudeMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    private sealed class ClaudeResponse
    {
        public IList<ClaudeContentBlock> Content { get; init; } = [];
        public string StopReason { get; init; } = string.Empty;
    }

    private sealed class ClaudeContentBlock
    {
        public string Type { get; init; } = string.Empty;
        public string? Name { get; init; }
        public JsonObject? Input { get; init; }
    }
}
