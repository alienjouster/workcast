namespace Workcast.Api.DTOs.Exchange;

/// <summary>
/// Portable representation of a job board and its scraper configuration, used for both export and import.
/// This is the shape written to <c>/community-boards/*.json</c> files and returned by the export endpoint.
/// User-specific fields (id, status, ad counts, timestamps) are intentionally excluded.
/// </summary>
public record BoardExchangeDto
{
    /// <summary>Schema version for forward-compatibility checks. Currently "1".</summary>
    public string SchemaVersion { get; init; } = "1";

    /// <summary>Display name of the job board.</summary>
    public required string Name { get; init; }

    /// <summary>Canonical seed URL used to identify and scrape this board.</summary>
    public required string Url { get; init; }

    /// <summary>Suggested cron expression for the scrape schedule. Importers may override this.</summary>
    public required string ScheduleCron { get; init; }

    /// <summary>Full scraper configuration including all selectors and pagination settings.</summary>
    public required ScraperConfigExchangeDto ScraperConfig { get; init; }
}
