namespace Workcast.Infrastructure.AI;

/// <summary>
/// Singleton that holds the result of the Anthropic API key health check performed at startup.
/// Injected into <see cref="Workcast.Api.Controllers.StatusController"/> so the frontend can
/// display a persistent error banner when the key is missing or invalid.
/// </summary>
public sealed class AnthropicHealthState
{
    public bool IsHealthy { get; private set; } = true;
    public string? ErrorMessage { get; private set; }

    public void SetHealthy()
    {
        IsHealthy = true;
        ErrorMessage = null;
    }

    public void SetError(string message)
    {
        IsHealthy = false;
        ErrorMessage = message;
    }
}
