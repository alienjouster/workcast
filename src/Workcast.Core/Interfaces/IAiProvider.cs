using Workcast.Core.Models;

namespace Workcast.Core.Interfaces;



/// <summary>
/// Abstracts all communication with an LLM provider.
/// All AI interactions are routed through this single interface so that the concrete
/// provider (Claude, OpenAI, etc.) can be swapped by updating the DI registration only.
/// Uses structured output via Tool Use — see TECHSPEC sections 4.1–4.3.
/// Job ad field extraction is fully deterministic using the CSS selectors returned by
/// <see cref="AnalyzeBoardAsync"/> — no per-ad AI call is made.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Analyzes a job board listing page and returns a structured scraping configuration
    /// including per-field CSS selectors for deterministic job ad extraction.
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
    /// Scores a resume against a job posting page and returns a structured breakdown
    /// of matched, partially-matched, and missing requirements.
    /// Uses Tool Use to guarantee structured output.
    /// </summary>
    /// <param name="resumeContent">Raw bytes of the resume file.</param>
    /// <param name="resumeContentType">MIME type: "application/pdf", "text/plain", or "application/json".</param>
    /// <param name="resumeFileName">Original file name (used as context label for text/JSON resumes).</param>
    /// <param name="jobPageText">Plain-text content of the job ad detail page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AdScoringResult> ScoreAdAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string jobPageText,
        CancellationToken ct = default);

    /// <summary>
    /// Generates an ATS-friendly tailored HTML resume by combining the candidate's resume,
    /// an HTML template (defines visual structure), the job ad, and the scoring analysis.
    /// The output is a complete HTML document using the template's structure with
    /// content tailored to the job ad.
    /// </summary>
    /// <param name="resumeContent">Raw bytes of the resume file.</param>
    /// <param name="resumeContentType">MIME type: "text/plain" or "application/json".</param>
    /// <param name="resumeFileName">Original file name (context label).</param>
    /// <param name="resumeTemplateHtml">HTML template defining the visual structure and CSS styling.</param>
    /// <param name="jobAdContent">Plain-text or HTML content of the job advertisement.</param>
    /// <param name="scoringSummary">AI scoring summary of the resume vs job ad match.</param>
    /// <param name="scoringRecommendation">AI scoring recommendation sentence.</param>
    /// <param name="scoringRequirementsJson">JSON array of scored requirements (name, category, score, notes).</param>
    /// <param name="optimizationLevel">Controls how aggressively content may be adapted to match the job ad.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A complete HTML document ready to render in a browser.</returns>
    Task<string> GenerateResumeAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string resumeTemplateHtml,
        string jobAdContent,
        string scoringSummary,
        string scoringRecommendation,
        string scoringRequirementsJson,
        ResumeOptimizationLevel optimizationLevel = ResumeOptimizationLevel.None,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a professional application letter (~half a page) in HTML format,
    /// tailored to the job ad and highlighting matched requirements from the scoring analysis.
    /// </summary>
    /// <param name="resumeContent">Raw bytes of the resume file.</param>
    /// <param name="resumeContentType">MIME type: "text/plain" or "application/json".</param>
    /// <param name="resumeFileName">Original file name (context label).</param>
    /// <param name="jobAdContent">Plain-text or HTML content of the job advertisement.</param>
    /// <param name="jobTitle">Job title for personalising the opening line.</param>
    /// <param name="company">Company name, or null if unknown.</param>
    /// <param name="scoringSummary">AI scoring summary of the resume vs job ad match.</param>
    /// <param name="scoringRecommendation">AI scoring recommendation sentence.</param>
    /// <param name="scoringRequirementsJson">JSON array of scored requirements (name, category, score, notes).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A complete HTML document (~half a page) ready to render in a browser.</returns>
    Task<string> GenerateLetterAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string jobAdContent,
        string? jobTitle,
        string? company,
        string scoringSummary,
        string scoringRecommendation,
        string scoringRequirementsJson,
        CancellationToken ct = default);
}
