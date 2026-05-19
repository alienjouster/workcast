namespace Workcast.Api.DTOs.Responses;

public record ApplicationStatsResponse
{
    // Counts
    public required int TotalApplications  { get; init; }
    public required int TotalSubmitted     { get; init; }  // Status != ToApply
    public required int TotalInterviewed   { get; init; }  // ever had Interviewing in StatusHistory
    public required int TotalHired         { get; init; }  // Status == ClosedHired

    // Rates (null when denominator == 0)
    public double? InterviewHitRatio       { get; init; }  // TotalInterviewed / TotalSubmitted * 100

    // Timing (days)
    public double? AverageDaysToApply      { get; init; }  // ScrapedAt → Applied.AchievedAt
    public double? AverageDaysToInterview  { get; init; }  // Applied.AchievedAt → Interviewing.AchievedAt

    // Interview logistics
    public double? AverageInterviewSteps   { get; init; }  // avg across apps with ≥1 step

    // Scoring
    public double? AverageScore            { get; init; }  // all scored apps
    public double? AverageScoreInterviewed { get; init; }  // scored apps that reached Interviewing

    // Distributions
    public required IList<StatusCountDto>             ApplicationsPerStatus { get; init; }
    public required IList<MonthlyApplicationCountDto> ApplicationsPerMonth  { get; init; }
}

public record StatusCountDto(string Status, int Count);

/// <summary>Application count for one calendar month.</summary>
/// <param name="Month">ISO year-month string, e.g. "2026-05".</param>
/// <param name="Count">Number of applications created in that month.</param>
public record MonthlyApplicationCountDto(string Month, int Count);
