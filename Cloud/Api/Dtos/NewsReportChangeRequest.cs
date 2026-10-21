using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// PATCH body for <c>newsreport/{id}</c>. Omitted (null) = unchanged.
/// </summary>
public class NewsReportChangeRequest
{
    /// <summary>Blank is ignored (a title is required).</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Calendar date <c>yyyy-MM-dd</c>. Anything else is a 400.</summary>
    [JsonPropertyName("release")]
    public string? Release { get; set; }

    /// <summary>TimeSpan, e.g. <c>0:06:30</c>. Invalid or negative is a 400.</summary>
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("explicit")]
    public bool? Explicit { get; set; }

    [JsonPropertyName("ignored")]
    public bool? Ignored { get; set; }

    /// <summary>true also deletes the report's search document.</summary>
    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    /// <summary>Replaces the curator subjects; [] clears. Dropped subjects are remembered as removed.</summary>
    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    /// <summary>"" clears.</summary>
    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    /// <summary>
    /// Guest person names; replaces the list. [] clears (stored as null). Blank names are dropped and
    /// names trimmed. Same rule as the episode edit API.
    /// </summary>
    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    /// <summary>"" or English clears (stored as null = English).</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
