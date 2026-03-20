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
}
