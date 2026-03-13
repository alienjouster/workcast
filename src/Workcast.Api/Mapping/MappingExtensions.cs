using Workcast.Api.DTOs.Responses;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Models;

namespace Workcast.Api.Mapping;

/// <summary>
/// Extension methods that map domain entities and models to their API response DTO equivalents.
/// All controllers use these methods exclusively — entities are never exposed directly.
/// </summary>
public static class MappingExtensions
{
    /// <summary>
    /// Maps a <see cref="JobBoard"/> entity to a <see cref="JobBoardResponse"/> DTO.
    /// </summary>
    /// <param name="board">The job board entity to map.</param>
    /// <param name="adCount">Pre-computed count of job ads for this board.</param>
    /// <param name="includeScraperConfig">When true, the <see cref="ScraperConfigResponse"/> is included in the response.</param>
    /// <returns>A populated <see cref="JobBoardResponse"/>.</returns>
    public static JobBoardResponse ToResponse(this JobBoard board, int adCount, bool includeScraperConfig = false)
    {
        return new JobBoardResponse
        {
            Id = board.Id,
            Name = board.Name,
            Url = board.Url,
            Status = board.Status.ToString().ToLowerInvariant(),
            ScheduleCron = board.ScheduleCron,
            LastScrapedAt = board.LastScrapedAt,
            CreatedAt = board.CreatedAt,
            UpdatedAt = board.UpdatedAt,
            AdCount = adCount,
            ScraperConfig = includeScraperConfig ? board.ScraperConfig?.ToResponse() : null,
        };
    }

    /// <summary>
    /// Maps a <see cref="JobAd"/> entity to a <see cref="JobAdResponse"/> DTO.
    /// </summary>
    /// <param name="ad">The job ad entity to map.</param>
    /// <returns>A populated <see cref="JobAdResponse"/>.</returns>
    public static JobAdResponse ToResponse(this JobAd ad)
    {
        return new JobAdResponse
        {
            Id = ad.Id,
            JobBoardId = ad.JobBoardId,
            ScrapeRunId = ad.ScrapeRunId,
            ExternalId = ad.ExternalId,
            Url = ad.Url,
            Title = ad.Title,
            Company = ad.Company,
            Location = ad.Location,
            SalaryRaw = ad.SalaryRaw,
            Description = ad.Description,
            PostedAt = ad.PostedAt,
            ScrapedAt = ad.ScrapedAt,
            IsActive = ad.IsActive,
        };
    }

    /// <summary>
    /// Maps a <see cref="ScrapeRun"/> entity to a <see cref="ScrapeRunResponse"/> DTO.
    /// </summary>
    /// <param name="run">The scrape run entity to map.</param>
    /// <returns>A populated <see cref="ScrapeRunResponse"/>.</returns>
    public static ScrapeRunResponse ToResponse(this ScrapeRun run)
    {
        return new ScrapeRunResponse
        {
            Id = run.Id,
            JobBoardId = run.JobBoardId,
            TriggeredBy = run.TriggeredBy.ToString().ToLowerInvariant(),
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            Status = run.Status.ToString().ToLowerInvariant(),
            PagesScraped = run.PagesScraped,
            AdsFound = run.AdsFound,
            AdsNew = run.AdsNew,
            Errors = run.Errors.Select(e => e.ToResponse()).ToList(),
        };
    }

    /// <summary>
    /// Maps a <see cref="ScraperConfig"/> model to a <see cref="ScraperConfigResponse"/> DTO.
    /// PaginationType enum values are converted to snake_case strings to match the JSON API contract
    /// defined in TECHSPEC section 3.5.
    /// </summary>
    /// <param name="config">The scraper config model to map.</param>
    /// <returns>A populated <see cref="ScraperConfigResponse"/>.</returns>
    public static ScraperConfigResponse ToResponse(this ScraperConfig config)
    {
        return new ScraperConfigResponse
        {
            PaginationType = ToSnakeCase(config.PaginationType),
            JobCardSelector = config.JobCardSelector,
            FieldSelectors = config.FieldSelectors.ToResponse(),
            NextPageSelector = config.NextPageSelector,
            UrlParamName = config.UrlParamName,
            UrlParamIsOffset = config.UrlParamIsOffset,
            MaxPages = config.MaxPages,
            RequiresJs = config.RequiresJs,
            SuggestedDelayMs = config.SuggestedDelayMs,
            ConfidenceScore = config.ConfidenceScore,
            AnalyzerNotes = config.AnalyzerNotes,
            GeneratedAt = config.GeneratedAt,
        };
    }

    /// <summary>
    /// Maps a <see cref="FieldSelectorMap"/> model to a <see cref="FieldSelectorMapResponse"/> DTO.
    /// </summary>
    /// <param name="map">The field selector map to map.</param>
    /// <returns>A populated <see cref="FieldSelectorMapResponse"/>.</returns>
    public static FieldSelectorMapResponse ToResponse(this FieldSelectorMap map)
    {
        return new FieldSelectorMapResponse
        {
            DetailUrl = map.DetailUrl,
            Title = map.Title,
            Company = map.Company,
            Location = map.Location,
            SalaryRaw = map.SalaryRaw,
            PostedAt = map.PostedAt,
            DescriptionSnippet = map.DescriptionSnippet,
            ExternalId = map.ExternalId,
        };
    }

    /// <summary>
    /// Maps a <see cref="ScrapeRunError"/> model to a <see cref="ScrapeRunErrorResponse"/> DTO.
    /// </summary>
    /// <param name="error">The scrape run error model to map.</param>
    /// <returns>A populated <see cref="ScrapeRunErrorResponse"/>.</returns>
    public static ScrapeRunErrorResponse ToResponse(this ScrapeRunError error)
    {
        return new ScrapeRunErrorResponse
        {
            Page = error.Page,
            Message = error.Message,
            Timestamp = error.Timestamp,
        };
    }

    /// <summary>
    /// Converts a <see cref="PaginationType"/> enum value to its snake_case string representation
    /// as required by TECHSPEC section 3.5 (e.g. UrlParam → "url_param").
    /// </summary>
    private static string ToSnakeCase(PaginationType paginationType)
    {
        return paginationType switch
        {
            PaginationType.UrlParam => "url_param",
            PaginationType.NextButton => "next_button",
            PaginationType.InfiniteScroll => "infinite_scroll",
            PaginationType.None => "none",
            // NOTE: Exhaustive match. Any future enum value added to PaginationType will cause a
            // compile warning; fall back to a safe lower-invariant conversion.
            _ => paginationType.ToString().ToLowerInvariant(),
        };
    }
}
