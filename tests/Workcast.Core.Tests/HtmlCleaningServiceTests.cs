using Workcast.Infrastructure.AI;

namespace Workcast.Core.Tests;

public class HtmlCleaningServiceTests
{
    private readonly HtmlCleaningService _sut = new();

    [Fact]
    public void CleanForBoardAnalysis_RemovesScriptTags()
    {
        var html = "<div>Hello</div><script>alert('x');</script><p>World</p>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("<script");
        result.Should().NotContain("alert");
        result.Should().Contain("Hello");
        result.Should().Contain("World");
    }

    [Fact]
    public void CleanForBoardAnalysis_RemovesStyleTags()
    {
        var html = "<style>.foo { color: red; }</style><div>Content</div>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("<style");
        result.Should().NotContain("color: red");
        result.Should().Contain("Content");
    }

    [Fact]
    public void CleanForBoardAnalysis_RemovesSvgTags()
    {
        var html = "<div>Before</div><svg xmlns='http://www.w3.org/2000/svg'><circle r='10'/></svg><div>After</div>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("<svg");
        result.Should().NotContain("<circle");
        result.Should().Contain("Before");
        result.Should().Contain("After");
    }

    [Fact]
    public void CleanForBoardAnalysis_RemovesHtmlComments()
    {
        var html = "<div>Visible</div><!-- secret comment --><p>Also visible</p>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("<!--");
        result.Should().NotContain("secret comment");
    }

    [Fact]
    public void CleanForBoardAnalysis_RemovesAriaAttributes()
    {
        var html = """<div aria-label="nav menu" aria-hidden="true" data-testid="main">Content</div>""";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("aria-label");
        result.Should().NotContain("aria-hidden");
    }

    [Fact]
    public void CleanForBoardAnalysis_PreservesDataAttributes()
    {
        var html = """<div data-testid="job-card" data-job-id="123">Job</div>""";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().Contain("data-testid");
        result.Should().Contain("data-job-id");
    }

    [Fact]
    public void CleanForBoardAnalysis_CollapsesWhitespace()
    {
        var html = "<div>Hello      World</div>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("      ");
    }

    [Fact]
    public void CleanForBoardAnalysis_CollapsesBlankLines()
    {
        var html = "<div>A</div>\n\n\n\n\n<div>B</div>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContainAll("\n\n\n");
    }

    [Fact]
    public void ExtractTextFromHtml_StripsAllHtmlTags()
    {
        var html = "<div><h1>Title</h1><p>Description</p></div>";

        var result = _sut.ExtractTextFromHtml(html);

        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().Contain("Title");
        result.Should().Contain("Description");
    }

    [Fact]
    public void ExtractTextFromHtml_DecodesHtmlEntities()
    {
        var html = "<p>Salt &amp; Pepper &lt;3</p>";

        var result = _sut.ExtractTextFromHtml(html);

        result.Should().Contain("Salt & Pepper <3");
    }

    [Fact]
    public void ExtractTextFromHtml_TruncatesAt20000Characters()
    {
        var html = "<p>" + new string('x', 25_000) + "</p>";

        var result = _sut.ExtractTextFromHtml(html);

        result.Length.Should().BeLessThanOrEqualTo(20_000);
    }

    [Fact]
    public void ExtractTextFromHtml_DoesNotTruncateShortContent()
    {
        var html = "<p>Short content</p>";

        var result = _sut.ExtractTextFromHtml(html);

        result.Should().Contain("Short content");
    }

    [Fact]
    public void ExtractTextFromHtml_TrimsResult()
    {
        var html = "  <p>  Trimmed  </p>  ";

        var result = _sut.ExtractTextFromHtml(html);

        result.Should().NotStartWith(" ");
        result.Should().NotEndWith(" ");
    }

    [Fact]
    public void CleanForBoardAnalysis_CaseInsensitiveTagRemoval()
    {
        var html = "<SCRIPT>bad()</SCRIPT><STYLE>.x{}</STYLE><SVG></SVG><div>ok</div>";

        var result = _sut.CleanForBoardAnalysis(html);

        result.Should().NotContain("bad()");
        result.Should().NotContain(".x{}");
        result.Should().Contain("ok");
    }
}
