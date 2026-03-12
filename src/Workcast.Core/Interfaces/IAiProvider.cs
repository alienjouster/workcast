using Workcast.Core.Models;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Abstracts all communication with an LLM provider.
/// All AI interactions are routed through this single interface so that the concrete
/// provider (Claude, OpenAI, etc.) can be swapped by updating the DI registration only.
/// Both methods use structured output via Tool Use — see TECHSPEC sections 4.1–4.4.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Analyzes a job board listing page and returns a structured scraping configuration.
    /// Called once when a job board is first registered.
    /// </summary>
    /// <param name="html">Pre-cleaned HTML of the job board listing page.</param>
    /// <param name="url">The canonical URL of the listing page (for context in the prompt).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BoardAnalysisResult"/> describing how to scrape this board.</returns>
    Task<BoardAnalysisResult> AnalyzeBoardAsync(
        string html,
        string url,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts structured job ad fields from a detail page's HTML.
    /// Called for each new job ad detail page encountered during a scrape run.
    /// </summary>
    /// <param name="html">Pre-cleaned HTML of the job ad detail page.</param>
    /// <param name="url">The canonical URL of the detail page (for context in the prompt).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="JobAdExtractionResult"/> containing the extracted fields.</returns>
    Task<JobAdExtractionResult> ExtractJobAdAsync(
        string html,
        string url,
        CancellationToken ct = default);
}
