namespace Workcast.Core.Entities;

/// <summary>
/// Global application settings stored in the database. Uses a singleton-row pattern:
/// exactly one row exists with <see cref="Id"/> = 1. Seeded on first migration.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Always 1 — only one settings row exists.</summary>
    public int Id { get; private set; }

    /// <summary>
    /// Anthropic model identifier used for board analysis AI calls.
    /// Overrides the value in <c>appsettings.json</c> at runtime.
    /// </summary>
    public string AiModel { get; private set; } = "claude-sonnet-4-5";

    // Required by EF Core.
    private AppSettings() { }

    /// <summary>Creates the singleton settings row with default values.</summary>
    public static AppSettings CreateDefault() => new() { Id = 1 };

    /// <summary>Updates the AI model identifier.</summary>
    public void SetAiModel(string model) => AiModel = model;
}
