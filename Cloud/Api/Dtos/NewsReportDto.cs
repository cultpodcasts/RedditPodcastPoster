using System.Text.Json.Serialization;

namespace Api.Dtos;

public class NewsReportDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("newsOrganisationId")]
    public Guid NewsOrganisationId { get; set; }

    [JsonPropertyName("newsOrganisationName")]
    public string? NewsOrganisationName { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>Calendar date, <c>yyyy-MM-dd</c> (news reports have no time of day).</summary>
    [JsonPropertyName("release")]
    public string? Release { get; set; }

    /// <summary><c>h:mm:ss</c>.</summary>
    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "";

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    public bool Removed { get; set; }

    [JsonPropertyName("tweeted")]
    public bool Tweeted { get; set; }

    [JsonPropertyName("blueskyPosted")]
    public bool BlueskyPosted { get; set; }

    [JsonPropertyName("subjects")]
    public string[] Subjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("hashTag")]
    public string? HashTag { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLinkDto>? Services { get; set; }
}
