using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Playable fields returned by the curator GET for a news report or TV-show episode.</summary>
public abstract class PlayableFieldsDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

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

    /// <summary>Guest person names, as on episodes. Null when there are none.</summary>
    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLinkDto>? Services { get; set; }
}
