namespace Workcast.Core.Interfaces;

/// <summary>
/// Abstracts the browser automation layer used to render JavaScript-heavy pages.
/// The concrete implementation uses Microsoft Playwright with a headless Chromium browser.
/// See TECHSPEC section 5.4 for configuration requirements.
/// </summary>
public interface IScraperEngine
{
    /// <summary>
    /// Renders a URL using a headless browser and returns the fully-rendered HTML.
    /// Waits for <c>networkidle</c> to ensure JavaScript frameworks have settled before
    /// returning the page source. When <paramref name="waitForSelector"/> is provided, also
    /// waits for a matching element to appear in the DOM before capturing the HTML — useful
    /// for SPAs (e.g. Workday) that populate the job list via a secondary API call after the
    /// initial network-idle event.
    /// </summary>
    /// <param name="url">The URL to render.</param>
    /// <param name="waitForSelector">
    /// Optional CSS selector to wait for after network-idle. When null, only the network-idle
    /// event is awaited. Times out after 15 seconds independently of the page load timeout.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fully-rendered HTML source of the page.</returns>
    Task<string> RenderPageAsync(
        string url,
        string? waitForSelector = null,
        CancellationToken ct = default);

    /// <summary>
    /// Renders a URL using a headless browser and returns the fully-rendered visible text,
    /// equivalent to <c>document.body.innerText</c>. No HTML tags, scripts, or attributes are
    /// included — only the text a user would read. Intended for AI scoring calls where clean
    /// plain text is required.
    /// </summary>
    /// <param name="url">The URL to render.</param>
    /// <param name="waitForSelector">
    /// Optional CSS selector to wait for after network-idle, same semantics as
    /// <see cref="RenderPageAsync"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The visible plain text of the rendered page.</returns>
    Task<string> RenderPageTextAsync(
        string url,
        string? waitForSelector = null,
        CancellationToken ct = default);

    /// <summary>
    /// Renders a URL using a headless browser, then repeatedly clicks a "load more" button
    /// that appends items to the current page without URL navigation. Stops when the button
    /// disappears from the DOM or <paramref name="maxClicks"/> is reached.
    /// Returns the final accumulated HTML containing all loaded items.
    /// </summary>
    /// <param name="url">The URL to render.</param>
    /// <param name="loadMoreSelector">CSS selector identifying the load-more button.</param>
    /// <param name="waitForSelector">
    /// Optional CSS selector to wait for after the initial page load (same semantics as
    /// <see cref="RenderPageAsync"/>).
    /// </param>
    /// <param name="maxClicks">Upper bound on the number of button clicks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fully-rendered HTML after all load-more clicks, containing all accumulated items.</returns>
    Task<string> RenderWithLoadMoreAsync(
        string url,
        string loadMoreSelector,
        string? waitForSelector = null,
        int maxClicks = 20,
        CancellationToken ct = default);
}
