using System.Text.Json.Serialization;

namespace Workcast.Core.Models;

/// <summary>
/// Controls how aggressively the resume generation AI may adapt the candidate's
/// content to better match the target job ad.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResumeOptimizationLevel
{
    /// <summary>
    /// No optimisation. Only information literally present in the resume is used.
    /// </summary>
    None = 0,

    /// <summary>
    /// Light optimisation. Words may be replaced by synonyms to improve keyword
    /// alignment. Changes are highlighted in orange.
    /// </summary>
    Light = 1,

    /// <summary>
    /// Medium optimisation. Skills and experiences may be reworded (not invented)
    /// to better reflect job-ad language. Changes are highlighted in orange.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Heavy optimisation. Similar skills or experiences may be added or rewritten
    /// to fill partial gaps. Changes are highlighted in orange.
    /// </summary>
    Heavy = 3,
}
