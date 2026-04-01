using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Workcast.Core.Interfaces;

namespace Workcast.Infrastructure.Scraping;

/// <summary>
/// Microsoft Playwright implementation of <see cref="IScraperEngine"/>.
/// Manages a single headless Chromium browser instance for the lifetime of the application.
/// All page rendering is sequential — no concurrent page processing per TECHSPEC section 5.4.
/// </summary>
public sealed class PlaywrightScraperEngine : IScraperEngine, IAsyncDisposable
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 800;
    private const int PageLoadTimeoutMs = 30_000;

    // Matches the Chromium version bundled with Playwright 1.44.
    // Deliberately omits "HeadlessChrome" which is the first string every bot-detector checks.
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger<PlaywrightScraperEngine> _logger;
    private bool _disposed;

    /// <summary>Initialises a new instance of <see cref="PlaywrightScraperEngine"/>.</summary>
    public PlaywrightScraperEngine(ILogger<PlaywrightScraperEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> RenderPageAsync(
        string url,
        string? waitForSelector = null,
        CancellationToken ct = default)
        => RenderAndExtractAsync(url, waitForSelector, p => p.ContentAsync(), ct);

    /// <inheritdoc />
    public Task<string> RenderPageTextAsync(
        string url,
        string? waitForSelector = null,
        CancellationToken ct = default)
        => RenderAndExtractAsync(url, waitForSelector, p => p.InnerTextAsync("body"), ct);

    private async Task<string> RenderAndExtractAsync(
        string url,
        string? waitForSelector,
        Func<IPage, Task<string>> extract,
        CancellationToken ct)
    {
        var browser = await GetBrowserAsync(ct).ConfigureAwait(false);

        await using var context = await NewStealthContextAsync(browser).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = PageLoadTimeoutMs,
            }).ConfigureAwait(false);

            // For JS-heavy SPAs (e.g. Workday) that populate the job list via a secondary
            // API call after the initial network-idle event, wait for the job card selector
            // to appear in the DOM before capturing the HTML.
            // Non-fatal: if the selector never appears (wrong config or static page),
            // proceed with whatever was already loaded.
            if (!string.IsNullOrEmpty(waitForSelector))
            {
                try
                {
                    await page.WaitForSelectorAsync(waitForSelector, new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = 15_000,
                    }).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // Selector did not appear within the extra budget — proceed with current content.
                }
            }

            return await extract(page).ConfigureAwait(false);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> RenderWithLoadMoreAsync(
        string url,
        string loadMoreSelector,
        string? waitForSelector = null,
        int maxClicks = 20,
        CancellationToken ct = default)
    {
        var browser = await GetBrowserAsync(ct).ConfigureAwait(false);

        await using var context = await NewStealthContextAsync(browser).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = PageLoadTimeoutMs,
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(waitForSelector))
            {
                try
                {
                    await page.WaitForSelectorAsync(waitForSelector, new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = 15_000,
                    }).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning(
                        "LoadMore: waitForSelector '{Selector}' did not appear within 15s after initial load",
                        waitForSelector);
                }
            }

            int initialItemCount = string.IsNullOrEmpty(waitForSelector) ? -1
                : await page.Locator(waitForSelector).CountAsync().ConfigureAwait(false);

            _logger.LogDebug(
                "LoadMore: page loaded. selector='{Selector}' maxClicks={MaxClicks} initialItems={InitialItems} url={Url}",
                loadMoreSelector, maxClicks, initialItemCount, url);

            for (int click = 0; click < maxClicks; click++)
            {
                ct.ThrowIfCancellationRequested();

                int buttonCount = await page.Locator(loadMoreSelector).CountAsync().ConfigureAwait(false);

                _logger.LogDebug(
                    "LoadMore: click attempt {Click}/{Max} — button count={Count}",
                    click + 1, maxClicks, buttonCount);

                if (buttonCount == 0)
                {
                    _logger.LogDebug("LoadMore: button not found in DOM — stopping");
                    break;
                }

                int itemsBefore = string.IsNullOrEmpty(waitForSelector) ? -1
                    : await page.Locator(waitForSelector).CountAsync().ConfigureAwait(false);

                _logger.LogDebug("LoadMore: dispatching MouseEvent (items before={ItemsBefore})", itemsBefore);

                // Dispatch a bubbling MouseEvent rather than calling .click() directly.
                // Many SPA frameworks (Vue, React) use event delegation and respond more
                // reliably to a properly constructed MouseEvent than to a synthetic .click().
                await page.EvalOnSelectorAsync(
                    loadMoreSelector,
                    "el => el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }))")
                    .ConfigureAwait(false);

                _logger.LogDebug("LoadMore: event dispatched, waiting for new items");

                if (itemsBefore >= 0)
                {
                    // Wait for the item count to increase — more reliable than networkidle
                    // because the DOM may update after the network goes idle.
                    // Timeout means no new items appeared: the button is exhausted.
                    try
                    {
                        var escapedSelector = waitForSelector!.Replace("'", "\\'");
                        await page.WaitForFunctionAsync(
                            $"() => document.querySelectorAll('{escapedSelector}').length > {itemsBefore}",
                            null,
                            new PageWaitForFunctionOptions { Timeout = 8_000 })
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogDebug(
                            "LoadMore: no new items after click {Click} — button exhausted, stopping",
                            click + 1);
                        break;
                    }
                }
                else
                {
                    // No item selector to count — fall back to networkidle.
                    try
                    {
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                            new PageWaitForLoadStateOptions { Timeout = 10_000 }).ConfigureAwait(false);
                    }
                    catch (TimeoutException) { }
                }

                int itemsAfter = string.IsNullOrEmpty(waitForSelector) ? -1
                    : await page.Locator(waitForSelector).CountAsync().ConfigureAwait(false);

                _logger.LogDebug(
                    "LoadMore: click {Click} done — items before={Before} after={After}",
                    click + 1, itemsBefore, itemsAfter);
            }

            int finalItemCount = string.IsNullOrEmpty(waitForSelector) ? -1
                : await page.Locator(waitForSelector).CountAsync().ConfigureAwait(false);

            _logger.LogDebug("LoadMore: loop finished — final item count={FinalItems}", finalItemCount);

            return await page.ContentAsync().ConfigureAwait(false);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
        _initLock.Dispose();
    }

    /// <summary>
    /// Creates a new browser context configured to resemble a real Chrome session:
    /// realistic User-Agent, client-hint headers, locale, timezone, and a script
    /// that removes the <c>navigator.webdriver</c> flag Chromium sets in headless mode.
    /// </summary>
    private static async Task<IBrowserContext> NewStealthContextAsync(IBrowser browser)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize  = new ViewportSize { Width = ViewportWidth, Height = ViewportHeight },
            UserAgent     = UserAgent,
            Locale        = "en-US",
            TimezoneId    = "America/New_York",
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"]           = "en-US,en;q=0.9",
                ["Accept"]                    = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
                ["sec-ch-ua"]                 = "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"",
                ["sec-ch-ua-mobile"]          = "?0",
                ["sec-ch-ua-platform"]        = "\"Windows\"",
                ["upgrade-insecure-requests"] = "1",
            },
        }).ConfigureAwait(false);

        // Runs before every page load in this context — hides the last detectable
        // automation signal that --disable-blink-features=AutomationControlled misses.
        await context.AddInitScriptAsync(
            "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });"
        ).ConfigureAwait(false);

        return context;
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock.
            if (_browser is not null)
            {
                return _browser;
            }

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                // Required for Docker: containers lack the Linux user-namespace capabilities
                // that Chrome's sandbox mechanism needs. Without --no-sandbox the zygote
                // process crashes with SIGBUS (exit code 135) immediately on launch.
                // --disable-dev-shm-usage makes Chrome write shared memory to /tmp instead
                // of /dev/shm, avoiding SIGBUS when Docker's default 64 MB /dev/shm fills up.
                Args =
                [
                    "--no-sandbox",
                    "--disable-dev-shm-usage",
                    // Removes the navigator.webdriver property and related automation
                    // flags that Chromium exposes when launched programmatically.
                    "--disable-blink-features=AutomationControlled",
                ],
            }).ConfigureAwait(false);

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
