namespace Workcast.Infrastructure.AI.Options;

/// <summary>
/// Strongly-typed configuration options for the Anthropic Claude API.
/// Bound from the "Anthropic" section of <c>appsettings.json</c> / environment variables.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>Configuration section name used for binding.</summary>
    public const string SectionName = "Anthropic";

    /// <summary>Anthropic API key (sk-ant-...).</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Claude model identifier. Defaults to claude-sonnet-4-5.</summary>
    public string Model { get; init; } = "claude-sonnet-4-5";
}
