using Workcast.Core.Interfaces;
using Workcast.Core.Models;

namespace Workcast.Infrastructure.AI;

/// <summary>
/// Implements <see cref="IJobBoardAnalyzer"/> by orchestrating the full board analysis pipeline:
/// renders the page via <see cref="IScraperEngine"/>, cleans the HTML via
/// <see cref="HtmlCleaningService"/>, then delegates to <see cref="IAiProvider"/>.
/// Consumed by <c>BoardAnalysisJob</c> in Workcast.Jobs.
/// </summary>
public sealed class AiExtractionService : IJobBoardAnalyzer
{
    private readonly IScraperEngine _scraperEngine;
    private readonly IAiProvider _aiProvider;
    private readonly HtmlCleaningService _htmlCleaner;

    /// <summary>
    /// Initializes a new <see cref="AiExtractionService"/>.
    /// </summary>
    public AiExtractionService(
        IScraperEngine scraperEngine,
        IAiProvider aiProvider,
        HtmlCleaningService htmlCleaner)
    {
        _scraperEngine = scraperEngine;
        _aiProvider = aiProvider;
        _htmlCleaner = htmlCleaner;
    }

    /// <inheritdoc />
    public async Task<BoardAnalysisResult> AnalyzeAsync(string url, CancellationToken ct = default)
    {
        var rawHtml = await _scraperEngine.RenderPageAsync(url, ct).ConfigureAwait(false);
        var cleanedHtml = _htmlCleaner.CleanForBoardAnalysis(rawHtml);
        return await _aiProvider.AnalyzeBoardAsync(cleanedHtml, url, ct).ConfigureAwait(false);
    }
}
