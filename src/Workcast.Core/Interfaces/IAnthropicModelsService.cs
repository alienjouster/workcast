namespace Workcast.Core.Interfaces;

/// <summary>
/// Represents an Anthropic model available for selection.
/// </summary>
public sealed record AvailableModelDto(string Id, string DisplayName);

/// <summary>
/// Returns the list of Claude models available from the Anthropic API.
/// Results are cached in-process to avoid redundant API calls.
/// </summary>
public interface IAnthropicModelsService
{
    Task<IReadOnlyList<AvailableModelDto>> GetModelsAsync(CancellationToken ct = default);
}
