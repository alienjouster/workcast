namespace Workcast.Core.Events;

/// <summary>
/// Represents a real-time event published by the Workcast backend and streamed to
/// connected browser clients via Server-Sent Events.
/// </summary>
public sealed record WorkcastEvent
{
    public const string BoardStatusChanged            = "boardStatusChanged";
    public const string RunEnqueued                   = "runEnqueued";
    public const string RunStarted                    = "runStarted";
    public const string RunStatusChanged              = "runStatusChanged";
    public const string RunCompleted                  = "runCompleted";
    public const string UnreadCountChanged            = "unreadCountChanged";
    public const string ScoringCompleted              = "scoringCompleted";
    public const string ApplicationScoringCompleted   = "applicationScoringCompleted";

    public required string Type          { get; init; }
    public Guid?   BoardId               { get; init; }
    public Guid?   RunId                 { get; init; }
    public Guid?   AdId                  { get; init; }
    public Guid?   ApplicationId         { get; init; }
    public string? Status                { get; init; }
    public int?    AdsNew                { get; init; }
    public int?    UnreadCount           { get; init; }
}
