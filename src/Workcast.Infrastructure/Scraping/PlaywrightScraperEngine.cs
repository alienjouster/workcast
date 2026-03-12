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

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;

    /// <inheritdoc />
    public async Task<string> RenderPageAsync(string url, CancellationToken ct = default)
    {
        var browser = await GetBrowserAsync(ct).ConfigureAwait(false);

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = ViewportWidth, Height = ViewportHeight },
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = PageLoadTimeoutMs,
            }).ConfigureAwait(false);

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
            }).ConfigureAwait(false);

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
