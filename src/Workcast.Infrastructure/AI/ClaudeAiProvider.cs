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
    private const int ScoringMaxTokens = 4096;
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
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<ClaudeAiProvider> _logger;

    /// <summary>
    /// Initializes a new <see cref="ClaudeAiProvider"/>.
    /// </summary>
    public ClaudeAiProvider(
        HttpClient httpClient,
        IOptions<AnthropicOptions> options,
        ISettingsRepository settingsRepository,
        ILogger<ClaudeAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _settingsRepository = settingsRepository;
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

            IMPORTANT — rules for every CSS selector you produce:

            1. A CSS selector is a string passed to document.querySelectorAll('...'). It uses
               element names (li, a, div), class names (.foo), IDs (#bar), and attribute filters
               ([attr="value"]). CSS selectors NEVER contain angle brackets < or > in the sense
               of HTML tags — do not write <li>, <a>, or any HTML markup as a selector.

            2. When using data-* attribute filters (e.g. [data-automation-id="..."]),
               use only values you can read in the HTML below — do not rely on training memory
               of what values a platform typically uses. This is especially important for
               Workday, Greenhouse, Lever and similar ATS where data-* values differ per tenant.

            3. job_card_selector must match MULTIPLE elements — one per job listing (typically
               5–50 results). Never use a single-occurrence wrapper such as #root, body, main,
               or [data-automation-id="jobSearchPage"] as the job card selector.

            4. No jQuery pseudo-classes: :contains(), :has() with text arguments, and similar
               non-standard pseudo-classes are forbidden. Use attribute or structural selectors
               instead (e.g. button[type="button"], [data-action="load-more"]).

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

            - How pagination works. Identify the pagination_type using these exact rules:
                url_param: the listing URL contains a page or offset query parameter (e.g. ?page=2,
                           ?start=20). Set url_param_name to the parameter name. Set
                           url_param_is_offset=true if it is an item offset rather than a page number.
                next_button: there is a "Next" link or button whose href attribute navigates to the
                             next page URL. Set next_page_selector to its CSS selector.
                load_more_button: there is a "Load more", "Show more", or similar button that, when
                                  clicked, appends more job listings to the current page WITHOUT
                                  navigating to a new URL. Set next_page_selector to its CSS selector.
                                  This is distinct from next_button — the key difference is that
                                  existing items remain on the page and new ones are added below them.
                                  Use a structural selector (tag, class, type attribute, DOM position)
                                  — never :contains().
                infinite_scroll: new listings load automatically as the user scrolls down, with no
                                 button to click.
                none: all listings are visible on a single page with no pagination mechanism.

            - Whether JavaScript is required to render the job listings

            - A safe request delay in milliseconds (be conservative — prefer 1000ms or more for
              sites that may rate-limit scrapers)

            HTML:
            {html}
            """;

        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);
        var input = await CallWithRetryAsync(prompt, tool, "save_board_config", settings.AiModel, ct).ConfigureAwait(false);
        return DeserializeBoardAnalysisResult(input);
    }

    /// <inheritdoc />
    public async Task<AdScoringResult> ScoreAdAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string jobPageText,
        CancellationToken ct = default)
    {
        var tool = BuildScoringTool();
        var settings = await _settingsRepository.GetAsync(ct).ConfigureAwait(false);

        var promptSuffix = $"""

            You are an expert executive helping the candidate to apply for an open position in a company.

            You will receive two inputs:
            - A JOB AD (job description of the open position you are recurting)
            - A RESUME (complete career history of the applicant)

            Your task is to:
            - Carefully analyze the JOB AD and identify required skills, experience, seniority, domain exposure, leadership scope, certifications, tools, and impact expectations
            - Carefully analyze the JSON RESUME skills, experience, seniority, domain exposure, leadership scope, education and certifications
            For each distinct skill, qualification, or requirement mentioned in the job posting:
            - category: "match" if clearly present in the resume, "partial_match" if partially covered, "gap" if absent
            - is_optional: true only if the posting explicitly says "nice to have", "preferred", "optional", "plus", or similar
            - score: 100 for match, 50 for partial_match, 0 for gap. For optional items, gaps may score 25 and partial matches up to 75.
            - notes: one short sentence explaining your reasoning

            "overall_score" is the arithmetic average of all requirement scores (0–100).
            "summary" is 1-2 short sentences describing the overall match quality factually.
            "recommendation" is 1 very short sentence advising whether the candidate should proceed with the application or not.

            Be very factual. Stick with the evidence given in the JOB AD and the RESUME.

            Call the submit_scoring tool with the complete analysis.

            JOB AD: {jobPageText}
            """;

        object userContent;

        if (resumeContentType == "application/pdf")
        {
            userContent = new object[]
            {
                new
                {
                    Type = "document",
                    Source = new
                    {
                        Type = "base64",
                        MediaType = "application/pdf",
                        Data = Convert.ToBase64String(resumeContent),
                    }
                },
                new { Type = "text", Text = promptSuffix },
            };
        }
        else
        {
            var resumeText = System.Text.Encoding.UTF8.GetString(resumeContent);
            userContent = $"RESUME ({resumeFileName}):\n\n{resumeText}\n\n{promptSuffix}";
        }

        var input = await CallScoringWithRetryAsync(userContent, tool, "submit_scoring", settings.AiModel, ct)
            .ConfigureAwait(false);

        return DeserializeAdScoringResult(input);
    }

    private async Task<JsonObject> CallScoringWithRetryAsync(
        object userContent,
        ClaudeTool tool,
        string toolName,
        string model,
        CancellationToken ct)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= MaxRetries - 1; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning(
                    "Anthropic scoring API call failed (attempt {Attempt}/{MaxRetries}), retrying in {Delay}s...",
                    attempt, MaxRetries, RetryDelays[attempt - 1].TotalSeconds);

                await Task.Delay(RetryDelays[attempt - 1], ct).ConfigureAwait(false);
            }

            try
            {
                var request = new
                {
                    Model = model,
                    MaxTokens = ScoringMaxTokens,
                    Temperature = 0,
                    Tools = new[] { tool },
                    ToolChoice = new ClaudeToolChoice { Type = "tool", Name = toolName },
                    Messages = new[]
                    {
                        new { Role = "user", Content = userContent },
                    },
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(120));

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
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Anthropic scoring API call attempt {Attempt} failed for tool {Tool}.", attempt + 1, toolName);
                lastException = ex;
            }
        }

        throw new InvalidOperationException(
            $"Anthropic scoring API call failed after {MaxRetries} attempts.", lastException);
    }

    private static AdScoringResult DeserializeAdScoringResult(JsonObject input)
    {
        var requirements = new List<AdScoringRequirementResult>();
        var reqs = input["requirements"]?.AsArray();

        if (reqs is not null)
        {
            foreach (var req in reqs)
            {
                if (req is not JsonObject r) continue;
                requirements.Add(new AdScoringRequirementResult
                {
                    Name = r["name"]?.GetValue<string>() ?? "",
                    Category = r["category"]?.GetValue<string>() ?? "gap",
                    IsOptional = r["is_optional"]?.GetValue<bool>() ?? false,
                    Score = r["score"]?.GetValue<double>() ?? 0.0,
                    Notes = r["notes"]?.GetValue<string?>(),
                });
            }
        }

        return new AdScoringResult
        {
            OverallScore = input["overall_score"]?.GetValue<double>() ?? 0.0,
            Summary = input["summary"]?.GetValue<string>() ?? "",
            Recommendation = input["recommendation"]?.GetValue<string>() ?? "",
            Requirements = requirements,
        };
    }

    private static ClaudeTool BuildScoringTool()
    {
        return new ClaudeTool
        {
            Name = "submit_scoring",
            Description = "Submit the structured scoring result after analyzing the resume against the job posting.",
            InputSchema = new
            {
                type = "object",
                required = new[] { "requirements", "overall_score", "summary", "recommendation" },
                properties = new
                {
                    requirements = new
                    {
                        type = "array",
                        description = "One entry per distinct skill, qualification, or requirement found in the job posting.",
                        items = new
                        {
                            type = "object",
                            required = new[] { "name", "category", "is_optional", "score" },
                            properties = new
                            {
                                name        = new { type = "string", description = "Short label for this requirement (e.g. 'React', '5 years C# experience')." },
                                category    = new { type = "string", @enum = new[] { "match", "partial_match", "gap" } },
                                is_optional = new { type = "boolean", description = "True only when the posting explicitly marks this as optional / nice to have." },
                                score       = new { type = "number", minimum = 0, maximum = 100 },
                                notes       = new { type = new[] { "string", "null" }, description = "One-sentence explanation." },
                            },
                        },
                    },
                    overall_score  = new { type = "number", minimum = 0, maximum = 100, description = "Arithmetic mean of all requirement scores." },
                    summary        = new { type = "string", description = "1–2 sentence factual narrative of the overall match quality." },
                    recommendation = new { type = "string", description = "1–2 actionable sentences advising whether to proceed and what to highlight or address." },
                },
            },
        };
    }

    private async Task<JsonObject> CallWithRetryAsync(
        string prompt,
        ClaudeTool tool,
        string toolName,
        string model,
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
                    Model = model,
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

    // Returns null if the selector is null/empty or contains HTML angle brackets,
    // which would cause a Playwright CSS parse error at runtime.
    private static string? SanitizeSelector(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || raw.Contains('<') ? null : raw;

    private static BoardAnalysisResult DeserializeBoardAnalysisResult(JsonObject input)
    {
        var paginationRaw = input["pagination_type"]?.GetValue<string>() ?? "none";
        var paginationType = paginationRaw switch
        {
            "url_param" => PaginationType.UrlParam,
            "next_button" => PaginationType.NextButton,
            "infinite_scroll" => PaginationType.InfiniteScroll,
            "load_more_button" => PaginationType.LoadMoreButton,
            _ => PaginationType.None,
        };

        var fieldSelectorsNode = input["field_selectors"] as JsonObject;

        var jobCardSelector = SanitizeSelector(input["job_card_selector"]?.GetValue<string>())
            ?? throw new InvalidOperationException("Claude did not return a valid job_card_selector.");

        return new BoardAnalysisResult
        {
            PaginationType = paginationType,
            JobCardSelector = jobCardSelector,
            FieldSelectors = new FieldSelectorMap
            {
                DetailUrl = SanitizeSelector(fieldSelectorsNode?["detail_url"]?.GetValue<string?>()),
                Title = SanitizeSelector(fieldSelectorsNode?["title"]?.GetValue<string?>()),
                Company = SanitizeSelector(fieldSelectorsNode?["company"]?.GetValue<string?>()),
                Location = SanitizeSelector(fieldSelectorsNode?["location"]?.GetValue<string?>()),
                SalaryRaw = SanitizeSelector(fieldSelectorsNode?["salary_raw"]?.GetValue<string?>()),
                PostedAt = SanitizeSelector(fieldSelectorsNode?["posted_at"]?.GetValue<string?>()),
                DescriptionSnippet = SanitizeSelector(fieldSelectorsNode?["description_snippet"]?.GetValue<string?>()),
                ExternalId = SanitizeSelector(fieldSelectorsNode?["external_id"]?.GetValue<string?>()),
            },
            NextPageSelector = SanitizeSelector(input["next_page_selector"]?.GetValue<string?>()),
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
                    pagination_type = new { type = "string", @enum = new[] { "url_param", "next_button", "infinite_scroll", "load_more_button", "none" }, description = "The pagination mechanism. Use load_more_button (not next_button) when a button appends items to the current page without URL navigation." },
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
                    next_page_selector = new { type = new[] { "string", "null" }, description = "Valid CSS3 selector (querySelectorAll-compatible, no jQuery extensions like :contains()) for the Next page link (next_button) or Load more button (load_more_button). Prefer structural selectors such as tag name, class, type attribute, or DOM position. Null for all other pagination types." },
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
