using Workcast.Core.Models;

namespace Workcast.Core.Interfaces;

/// <summary>
/// Orchestrates the full board analysis pipeline for a given URL:
/// renders the page via <see cref="IScraperEngine"/>, cleans the HTML,
/// and delegates to <see cref="IAiProvider"/> to produce a <see cref="BoardAnalysisResult"/>.
/// Implemented in Workcast.Infrastructure; consumed by the board analysis Hangfire job.
/// </summary>
public interface IJobBoardAnalyzer
{
    /// <summary>
    /// Performs a full analysis of a job board URL and returns a scraping configuration.
    /// </summary>
    /// <param name="url">The seed URL of the job board to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BoardAnalysisResult"/> ready to be persisted as a <see cref="Workcast.Core.Models.ScraperConfig"/>.</returns>
    Task<BoardAnalysisResult> AnalyzeAsync(string url, CancellationToken ct = default);
}
