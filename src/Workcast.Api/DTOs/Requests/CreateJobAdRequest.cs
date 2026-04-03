using System.ComponentModel.DataAnnotations;

namespace Workcast.Api.DTOs.Requests;

/// <summary>
/// Request body for manually creating a job ad.
/// </summary>
public record CreateJobAdRequest
{
    /// <summary>Gets the URL of the job ad.</summary>
    [Required]
    [MaxLength(2048)]
    public required string Url { get; init; }

    /// <summary>Gets the job title.</summary>
    [Required]
    [MaxLength(512)]
    public required string Title { get; init; }

    /// <summary>Gets the optional company name.</summary>
    [MaxLength(255)]
    public string? Company { get; init; }

    /// <summary>Gets the optional location.</summary>
    [MaxLength(255)]
    public string? Location { get; init; }
}
