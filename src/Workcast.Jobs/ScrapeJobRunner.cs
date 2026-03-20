using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Workcast.Core.Entities;
using Workcast.Core.Enums;
using Workcast.Core.Interfaces;
using Workcast.Core.Models;
using Workcast.Infrastructure.Persistence;
using Workcast.Infrastructure.Scheduling;

namespace Workcast.Jobs;

/// <summary>
/// Hangfire job that executes the full scraping pipeline for a registered job board.
/// Runs as a recurring job (on the board's cron schedule) and as a fire-and-forget job
/// when triggered manually via the API refresh endpoint.
/// <para>
/// Pipeline: load board → create run → listing loop (render, extract job cards, paginate)
/// → per-card deterministic field extraction via CSS selectors → deduplication → persist
/// → stale detection.
/// </para>
/// Job ad fields are extracted from each job card element using the <see cref="FieldSelectorMap"/>
/// stored in <see cref="ScraperConfig.FieldSelectors"/>. No per-ad AI call is made.
/// See TECHSPEC sections 5.2, 5.3 for the full pipeline specification.
/// </summary>
public sealed class ScrapeJobRunner
{
    /// <summary>
    /// Global hard cap on pages scraped per run when <see cref="ScraperConfig.MaxPages"/> is null.
    /// Guards against infinite pagination loops. See TECHSPEC section 13.1.
    /// </summary>
    private const int GLOBAL_MAX_PAGES = 100;

    private readonly AppDbContext _dbContext;
    private readonly IScraperEngine _scraperEngine;
    private readonly HangfireJobScheduler _jobScheduler;
    private readonly ILogger<ScrapeJobRunner> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="ScrapeJobRunner"/>.
    /// </summary>
    /// <param name="dbContext">EF Core database context (scoped per job execution).</param>
    /// <param name="scraperEngine">Playwright-backed page renderer.</param>
    /// <param name="jobScheduler">Hangfire scheduling wrapper.</param>
    /// <param name="logger">Logger for this job.</param>
    public ScrapeJobRunner(
        AppDbContext dbContext,
        IScraperEngine scraperEngine,
        HangfireJobScheduler jobScheduler,
        ILogger<ScrapeJobRunner> logger)
    {
        _dbContext = dbContext;
        _scraperEngine = scraperEngine;
        _jobScheduler = jobScheduler;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full scrape run pipeline for the specified job board.
    /// Creates a <see cref="ScrapeRun"/> record, processes all listing pages with
    /// pagination, extracts and persists new job ads from job card elements, then runs
    /// stale detection.
    /// </summary>
    /// <param name="jobBoardId">The ID of the job board to scrape.</param>
    /// <param name="triggerSource">Whether this run was triggered by the scheduler or manually.</param>
    /// <param name="ct">Cancellation token passed by Hangfire.</param>
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync(
        Guid jobBoardId,
        TriggerSource triggerSource = TriggerSource.Scheduler,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting scrape run for board {BoardId} (trigger: {Trigger})",
            jobBoardId, triggerSource);

        var board = await _dbContext.JobBoards
            .FirstOrDefaultAsync(b => b.Id == jobBoardId, ct)
            .ConfigureAwait(false);

        if (board is null)
        {
            _logger.LogWarning("Board {BoardId} not found — scrape aborted", jobBoardId);
            return;
        }

        if (board.Status == BoardStatus.Paused)
        {
            _logger.LogInformation("Board {BoardId} is paused — skipping run", jobBoardId);
            return;
        }

        if (board.ScraperConfig is null)
        {
            _logger.LogWarning(
                "Board {BoardId} has no ScraperConfig — analysis may still be pending",
                jobBoardId);
            return;
        }

        var run = ScrapeRun.Create(jobBoardId, triggerSource);
        _dbContext.ScrapeRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        var config = board.ScraperConfig;
        var seenNormalizedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var counters = new RunCounters();

        try
        {
            await ExecuteListingLoopAsync(
                board, run, config, seenNormalizedUrls, counters,
                ct).ConfigureAwait(false);

            if (run.Errors.Count > 0)
                run.CompletePartial(counters.PagesScraped, counters.AdsFound, counters.AdsNew);
            else
                run.Complete(counters.PagesScraped, counters.AdsFound, counters.AdsNew);

            board.RecordScrapeCompleted();
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Scrape run {RunId} completed: {Pages} pages, {Found} found, {New} new, {Errors} errors",
                run.Id, counters.PagesScraped, counters.AdsFound, counters.AdsNew, run.Errors.Count);

            await MarkStaleAdsAsync(jobBoardId, seenNormalizedUrls, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Persist the partial failure state using a fresh token since the original is cancelled.
            run.Fail(counters.PagesScraped, counters.AdsFound, counters.AdsNew);
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fatal error in scrape run {RunId} for board {BoardId}",
                run.Id, jobBoardId);

            run.Fail(counters.PagesScraped, counters.AdsFound, counters.AdsNew);
            board.SetError();
            await _dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            // Re-throw so Hangfire records the failure and applies its retry policy.
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Listing loop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mutable counters threaded through the listing loop. Using a class avoids
    /// <c>ref</c> parameters, which are not permitted on async methods in C#.
    /// </summary>
    private sealed class RunCounters
    {
        public int PagesScraped { get; set; }
        public int AdsFound { get; set; }
        public int AdsNew { get; set; }
    }

    /// <summary>
    /// Drives the main listing and pagination loop. Renders each listing page, extracts
    /// job card elements and their fields via CSS selectors, processes each card, then
    /// advances to the next page.
    /// </summary>
    private async Task ExecuteListingLoopAsync(
        JobBoard board,
        ScrapeRun run,
        ScraperConfig config,
        HashSet<string> seenNormalizedUrls,
        RunCounters counters,
        CancellationToken ct)
    {
        int maxPages = config.MaxPages ?? GLOBAL_MAX_PAGES;
        string currentUrl = board.Url;
        int pageNumber = 1;
        int urlParamOffset = 0;

        while (counters.PagesScraped < maxPages)
        {
            // For url_param pagination, always build the URL from the board's base URL
            // to avoid query-string accumulation across iterations.
            string pageUrl = config.PaginationType == PaginationType.UrlParam
                ? BuildUrlParamUrl(board.Url, config, pageNumber, urlParamOffset)
                : currentUrl;

            _logger.LogDebug("Rendering listing page {Number}: {Url}", pageNumber, pageUrl);

            string html;
            try
            {
                html = await _scraperEngine.RenderPageAsync(pageUrl, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to render listing page {Url}", pageUrl);
                run.AddError(pageUrl, ex.Message);
                // Cannot determine next page without the current page HTML — stop pagination.
                break;
            }

            counters.PagesScraped++;

            var cards = ExtractAdsFromPage(html, config, pageUrl);
            int cardsOnPage = cards.Count;

            // If the first page returns no cards the selector is likely stale — log and stop.
            // Re-analysis must be triggered manually via the Re-analyze button.
            if (cardsOnPage == 0 && pageNumber == 1)
            {
                _logger.LogWarning(
                    "Selector matched 0 cards on first page for board {BoardId}. " +
                    "The scraper config may be stale — use the Re-analyze action to regenerate it.",
                    board.Id);
                return;
            }

            counters.AdsFound += cardsOnPage;

            _logger.LogDebug("Extracted {Count} job cards on page {Number}", cardsOnPage, pageNumber);

            foreach (var card in cards)
            {
                var normalizedUrl = NormalizeUrl(card.Url);
                seenNormalizedUrls.Add(normalizedUrl);

                bool isNew = await ProcessAdCardAsync(card, normalizedUrl, board.Id, run, ct)
                    .ConfigureAwait(false);

                if (isNew) counters.AdsNew++;

                if (config.SuggestedDelayMs > 0)
                    await Task.Delay(config.SuggestedDelayMs, ct).ConfigureAwait(false);
            }

            // Determine the next page URL.
            string? nextUrl = GetNextPageUrl(
                html, config, pageUrl, board.Url, pageNumber, cardsOnPage,
                ref urlParamOffset);

            if (nextUrl is null) break;

            currentUrl = nextUrl;
            pageNumber++;
        }
    }

    // -------------------------------------------------------------------------
    // Ad processing
    // -------------------------------------------------------------------------

    /// <summary>
    /// A job ad extracted from a listing page, containing all fields resolved via CSS selectors.
    /// </summary>
    private sealed record ExtractedAdData(
        string Url,
        string? Title,
        string? Company,
        string? Location,
        string? SalaryRaw,
        string? PostedAt,
        string? ExternalId,
        string? DescriptionSnippet);

    /// <summary>
    /// Processes a single extracted ad: checks deduplication and, if new, persists the ad with
    /// the field values already extracted via CSS selectors.
    /// Returns <c>true</c> if a new ad was created; <c>false</c> if the ad already existed.
    /// Non-fatal errors are logged to the run's error list rather than aborting the run.
    /// </summary>
    /// <param name="card">Extracted ad data with all field values.</param>
    /// <param name="normalizedUrl">URL with query string stripped, used for deduplication and storage.</param>
    /// <param name="boardId">Owning board ID.</param>
    /// <param name="run">Current scrape run, used to log non-fatal errors.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<bool> ProcessAdCardAsync(
        ExtractedAdData card,
        string normalizedUrl,
        Guid boardId,
        ScrapeRun run,
        CancellationToken ct)
    {
        // Primary deduplication: normalised URL match.
        var existingByUrl = await _dbContext.JobAds
            .FirstOrDefaultAsync(a => a.JobBoardId == boardId && a.Url == normalizedUrl, ct)
            .ConfigureAwait(false);

        if (existingByUrl is not null)
        {
            // Re-activate a previously stale ad if it reappears on the board.
            if (!existingByUrl.IsActive)
            {
                existingByUrl.MarkActive();
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogDebug("Re-activated stale ad {AdId} ({Url})", existingByUrl.Id, normalizedUrl);
            }

            return false;
        }

        // Secondary deduplication: ExternalId match (board-specific job reference numbers).
        if (!string.IsNullOrEmpty(card.ExternalId))
        {
            var existingByExternalId = await _dbContext.JobAds
                .FirstOrDefaultAsync(
                    a => a.JobBoardId == boardId && a.ExternalId == card.ExternalId,
                    ct)
                .ConfigureAwait(false);

            if (existingByExternalId is not null)
            {
                if (!existingByExternalId.IsActive)
                {
                    existingByExternalId.MarkActive();
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                return false;
            }
        }

        var ad = JobAd.Create(boardId, normalizedUrl, run.Id);
        ad.ApplyExtraction(
            card.Title,
            card.Company,
            card.Location,
            card.SalaryRaw,
            card.PostedAt,
            card.ExternalId,
            card.DescriptionSnippet);

        _dbContext.JobAds.Add(ad);

        try
        {
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent run may have inserted the same URL between our dedup check and
            // the save. Log as a non-fatal warning and skip rather than failing the run.
            _logger.LogWarning(ex,
                "Duplicate key conflict saving ad {Url} — likely inserted by a concurrent run",
                normalizedUrl);
            _dbContext.Entry(ad).State = EntityState.Detached;
            return false;
        }

        _logger.LogDebug("Saved new ad '{Title}' ({Url})", ad.Title, normalizedUrl);

        return true;
    }

    // -------------------------------------------------------------------------
    // Stale detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Marks active job ads as inactive when their URL was not observed during this run.
    /// <para>
    /// NOTE: TECHSPEC section 5.3 specifies stale detection after 1 consecutive missed runs.
    /// The <see cref="JobAd"/> entity does not carry a <c>LastSeenAt</c> or
    /// <c>ConsecutiveMissCount</c> field — only the run that first discovered the ad
    /// (<c>ScrapeRunId</c>). Since <c>Workcast.Core</c> is locked, implementing the full
    /// 1-run window would require a schema change. This implementation marks ads inactive
    /// after a single missed run and restores them via <see cref="JobAd.MarkActive"/> when
    /// they reappear. The observable behaviour matches the spec for the common case.
    /// </para>
    /// </summary>
    /// <param name="boardId">The board whose ads to evaluate.</param>
    /// <param name="seenNormalizedUrls">Normalised URLs encountered during this run.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task MarkStaleAdsAsync(
        Guid boardId,
        HashSet<string> seenNormalizedUrls,
        CancellationToken ct)
    {
        var activeAds = await _dbContext.JobAds
            .Where(a => a.JobBoardId == boardId && a.IsActive)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var staleAds = activeAds
            .Where(a => !seenNormalizedUrls.Contains(a.Url))
            .ToList();

        if (staleAds.Count == 0) return;

        foreach (var ad in staleAds)
            ad.MarkInactive();

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Marked {Count} ads as inactive (stale) for board {BoardId}",
            staleAds.Count, boardId);
    }

    // -------------------------------------------------------------------------
    // Pagination helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Determines the URL for the next listing page based on the pagination type.
    /// Returns <c>null</c> when there are no more pages.
    /// </summary>
    /// <param name="html">HTML of the current listing page.</param>
    /// <param name="config">Active scraper configuration.</param>
    /// <param name="currentPageUrl">Fully qualified URL of the current page.</param>
    /// <param name="boardBaseUrl">The board's root URL, used as the base for url_param construction.</param>
    /// <param name="currentPageNumber">1-based page counter.</param>
    /// <param name="cardsFoundOnPage">Number of job cards found on the current page.</param>
    /// <param name="urlParamOffset">Mutable offset value for offset-based url_param pagination.</param>
    private string? GetNextPageUrl(
        string html,
        ScraperConfig config,
        string currentPageUrl,
        string boardBaseUrl,
        int currentPageNumber,
        int cardsFoundOnPage,
        ref int urlParamOffset)
    {
        switch (config.PaginationType)
        {
            case PaginationType.UrlParam when config.UrlParamName is not null:
                // Stop when a page returned no cards — the parameter has gone past the last page.
                if (cardsFoundOnPage == 0) return null;

                if (config.UrlParamIsOffset)
                    urlParamOffset += cardsFoundOnPage;

                // The next URL is always built from the board base URL so the param
                // is the only query-string mutation; BuildUrlParamUrl handles construction.
                return BuildUrlParamUrl(boardBaseUrl, config, currentPageNumber + 1, urlParamOffset);

            case PaginationType.NextButton when config.NextPageSelector is not null:
                return GetNextButtonUrl(html, config.NextPageSelector, currentPageUrl);

            case PaginationType.InfiniteScroll:
                // NOTE: IScraperEngine.RenderPageAsync renders the page once and returns its
                // initial HTML. Programmatic scrolling is not supported through this interface.
                // Infinite scroll pagination therefore behaves as a single page in this
                // implementation. A future enhancement could add a scroll-capable method to
                // IScraperEngine. See TECHSPEC section 5.2 and AGENTS.md agent boundary rules.
                return null;

            case PaginationType.None:
            default:
                return null;
        }
    }

    /// <summary>
    /// Builds a listing page URL by setting the pagination query parameter on the board's
    /// base URL. For offset-based pagination (<see cref="ScraperConfig.UrlParamIsOffset"/>),
    /// <paramref name="urlParamOffset"/> is used directly; otherwise the 1-based
    /// <paramref name="pageNumber"/> is used.
    /// </summary>
    /// <param name="baseUrl">The board's seed URL.</param>
    /// <param name="config">Active scraper configuration providing the param name and mode.</param>
    /// <param name="pageNumber">1-based page counter.</param>
    /// <param name="urlParamOffset">Current item offset for offset-based pagination.</param>
    private static string BuildUrlParamUrl(
        string baseUrl,
        ScraperConfig config,
        int pageNumber,
        int urlParamOffset)
    {
        if (config.UrlParamName is null) return baseUrl;

        var uriBuilder = new UriBuilder(baseUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);

        int paramValue = config.UrlParamIsOffset ? urlParamOffset : pageNumber;
        query[config.UrlParamName] = paramValue.ToString();
        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Parses the current page's HTML and extracts the href of the next-page button
    /// identified by <paramref name="selector"/>. Returns <c>null</c> when the button is
    /// absent, disabled, or carries no href.
    /// </summary>
    /// <param name="html">HTML of the current listing page.</param>
    /// <param name="selector">CSS selector identifying the next-page button.</param>
    /// <param name="baseUrl">Base URL for resolving relative hrefs.</param>
    private static string? GetNextButtonUrl(string html, string selector, string baseUrl)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var nextButton = document.QuerySelector(selector);

        if (nextButton is null) return null;

        // Treat explicit disabled attribute or "disabled" CSS class as end of pagination.
        if (nextButton.HasAttribute("disabled") ||
            nextButton.ClassList.Contains("disabled") ||
            nextButton.ClassList.Contains("is-disabled"))
        {
            return null;
        }

        var href = nextButton.GetAttribute("href");
        if (string.IsNullOrWhiteSpace(href)) return null;

        if (Uri.TryCreate(new Uri(baseUrl), href, out var absolute))
            return absolute.AbsoluteUri;

        return null;
    }

    // -------------------------------------------------------------------------
    // Card extraction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts job ads from a listing page using the CSS selectors in the scraper config.
    /// For each element matched by <see cref="ScraperConfig.JobCardSelector"/>, applies
    /// <see cref="ScraperConfig.FieldSelectors"/> to resolve the detail URL and all
    /// extractable fields.
    /// <para>
    /// Detail URL resolution: uses <see cref="FieldSelectorMap.DetailUrl"/> when set;
    /// otherwise falls back to the first <c>&lt;a&gt;</c> element within the card.
    /// Cards where no href can be resolved are skipped.
    /// </para>
    /// </summary>
    /// <param name="html">HTML of the listing page.</param>
    /// <param name="config">Active scraper configuration providing card and field selectors.</param>
    /// <param name="baseUrl">Base URL for resolving relative hrefs.</param>
    /// <returns>Distinct list of extracted ad data with resolved field values.</returns>
    private static IReadOnlyList<ExtractedAdData> ExtractAdsFromPage(
        string html,
        ScraperConfig config,
        string baseUrl)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(html);
        var cardElements = document.QuerySelectorAll(config.JobCardSelector);
        var fieldSelectors = config.FieldSelectors;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cards = new List<ExtractedAdData>();

        foreach (var card in cardElements)
        {
            // Resolve the detail URL via the configured selector, or fall back to the first <a>.
            string? detailUrl = null;
            var baseUri = new Uri(baseUrl);

            if (fieldSelectors.DetailUrl is not null)
            {
                var href = card.QuerySelector(fieldSelectors.DetailUrl)?.GetAttribute("href");
                if (!string.IsNullOrWhiteSpace(href) &&
                    Uri.TryCreate(baseUri, href, out var absolute))
                {
                    detailUrl = absolute.AbsoluteUri;
                }
            }

            // Fallback: the card element itself if it is an <a>, otherwise the first <a> inside it.
            // When the JobCardSelector targets <a> elements directly (e.g. "a.JobCard"), the card
            // IS the anchor — QuerySelector("a") would search for a descendant and find nothing.
            if (detailUrl is null)
            {
                var anchor = card.TagName.Equals("A", StringComparison.OrdinalIgnoreCase)
                    ? card
                    : card.QuerySelector("a");
                var href = anchor?.GetAttribute("href");
                if (!string.IsNullOrWhiteSpace(href) &&
                    Uri.TryCreate(baseUri, href, out var absolute))
                {
                    detailUrl = absolute.AbsoluteUri;
                }
            }

            if (string.IsNullOrWhiteSpace(detailUrl)) continue;
            if (!seen.Add(detailUrl)) continue; // Deduplicate within page.

            cards.Add(new ExtractedAdData(
                Url: detailUrl,
                Title: GetElementText(card, fieldSelectors.Title),
                Company: GetElementText(card, fieldSelectors.Company),
                Location: GetElementText(card, fieldSelectors.Location),
                SalaryRaw: GetElementText(card, fieldSelectors.SalaryRaw),
                PostedAt: GetElementText(card, fieldSelectors.PostedAt),
                ExternalId: GetElementText(card, fieldSelectors.ExternalId),
                DescriptionSnippet: GetElementText(card, fieldSelectors.DescriptionSnippet)));
        }

        return cards;
    }

    /// <summary>
    /// Evaluates a CSS selector relative to <paramref name="element"/> and returns the matched
    /// element's trimmed text content. Returns <c>null</c> when the selector is null, the
    /// element is not found, or the text content is empty.
    /// </summary>
    /// <param name="element">Root element to query from.</param>
    /// <param name="selector">CSS selector, or null to skip.</param>
    private static string? GetElementText(IElement element, string? selector)
    {
        if (selector is null) return null;
        var text = element.QuerySelector(selector)?.TextContent.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // -------------------------------------------------------------------------
    // URL normalisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Normalises a URL for deduplication by stripping the query string and fragment,
    /// then lowercasing the scheme and host. The normalised form is used both as the
    /// database storage value for <see cref="JobAd.Url"/> and as the deduplication key.
    /// See TECHSPEC section 5.3 (primary deduplication strategy).
    /// </summary>
    /// <param name="url">Raw URL extracted from the listing page.</param>
    /// <returns>Normalised URL without query string or fragment.</returns>
    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.Trim();

        // Reconstruct with scheme + host + path only, lowercased.
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{uri.AbsolutePath}";
    }
}
