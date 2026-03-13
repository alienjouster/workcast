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
    /// returning the page source.
    /// </summary>
    /// <param name="url">The URL to render.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fully-rendered HTML source of the page.</returns>
    Task<string> RenderPageAsync(string url, CancellationToken ct = default);
}
