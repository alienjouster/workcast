using System.Text.RegularExpressions;

namespace Workcast.Infrastructure.AI;

/// <summary>
/// Stateless service that reduces raw Playwright HTML to a compact form suitable for
/// Claude API calls. The cleaning pipeline targets under 10,000 tokens per call by
/// removing non-content elements and collapsing whitespace.
/// See TECHSPEC sections 4.5 and 10.2 for the cost rationale.
/// </summary>
public sealed class HtmlCleaningService
{
    // Pre-compiled regexes for performance (this service is Singleton).
    private static readonly Regex ScriptTagRegex = new(
        @"<script\b[^>]*>[\s\S]*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StyleTagRegex = new(
        @"<style\b[^>]*>[\s\S]*?</style>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SvgTagRegex = new(
        @"<svg\b[^>]*>[\s\S]*?</svg>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlCommentRegex = new(
        @"<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    private static readonly Regex DataAttributeRegex = new(
        @"\s+data-[\w-]+=(?:""[^""]*""|'[^']*'|\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AriaAttributeRegex = new(
        @"\s+aria-[\w-]+=(?:""[^""]*""|'[^']*'|\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WhitespaceCollapseRegex = new(
        @"[ \t]{2,}",
        RegexOptions.Compiled);

    private static readonly Regex BlankLineCollapseRegex = new(
        @"(\r?\n){3,}",
        RegexOptions.Compiled);

    // Heuristic: try to find the main content block for ad detail pages.
    private static readonly Regex MainContentRegex = new(
        @"<(?:main|article)(?:\s[^>]*)?>[\s\S]*?</(?:main|article)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Cleans a raw HTML string for use in board analysis prompts.
    /// Removes scripts, styles, SVGs, comments, and non-essential attributes,
    /// then collapses whitespace.
    /// </summary>
    /// <param name="html">Raw HTML from Playwright.</param>
    /// <returns>Cleaned HTML suitable for the board analysis Claude call.</returns>
    public string CleanForBoardAnalysis(string html)
    {
        return ApplyBaseCleaning(html);
    }

    /// <summary>
    /// Cleans a raw HTML string for use in job ad extraction prompts.
    /// Applies the base cleaning pipeline and additionally attempts to isolate
    /// the main content subtree using <c>&lt;main&gt;</c> or <c>&lt;article&gt;</c> heuristics,
    /// which significantly reduces token usage for detail pages.
    /// </summary>
    /// <param name="html">Raw HTML from Playwright.</param>
    /// <returns>Cleaned HTML, narrowed to main content where possible.</returns>
    public string CleanForAdExtraction(string html)
    {
        var cleaned = ApplyBaseCleaning(html);

        // Attempt to isolate the main content block. If found, use only that subtree
        // to reduce token count further. Fall back to the full cleaned HTML if not found.
        var match = MainContentRegex.Match(cleaned);
        return match.Success ? match.Value : cleaned;
    }

    private static string ApplyBaseCleaning(string html)
    {
        var result = html;

        result = ScriptTagRegex.Replace(result, string.Empty);
        result = StyleTagRegex.Replace(result, string.Empty);
        result = SvgTagRegex.Replace(result, string.Empty);
        result = HtmlCommentRegex.Replace(result, string.Empty);
        result = DataAttributeRegex.Replace(result, string.Empty);
        result = AriaAttributeRegex.Replace(result, string.Empty);
        result = WhitespaceCollapseRegex.Replace(result, " ");
        result = BlankLineCollapseRegex.Replace(result, "\n\n");

        return result.Trim();
    }
}
