using Workcast.Core.Models;

namespace Workcast.Api.DTOs.Requests;

public record GenerateResumeRequest
{
    public ResumeOptimizationLevel OptimizationLevel { get; init; } = ResumeOptimizationLevel.None;
}
