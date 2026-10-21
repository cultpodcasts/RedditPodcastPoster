using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Curator GET for a film: the shared publisher fields, the IMDb canonical id and the playable fields a film has.
/// <c>release</c> is <c>yyyy</c> for a year-only release, otherwise <c>yyyy-MM-dd</c>.
/// </summary>
public class FilmDto : PublisherFieldsDto
{
    [JsonPropertyName("imdb")]
    public Uri? Imdb { get; set; }

    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }

    [JsonPropertyName("release")]
    public string? Release { get; set; }

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = "";

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("posted")]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool Tweeted { get; set; }

    [JsonPropertyName("blueskyPosted")]
    public bool BlueskyPosted { get; set; }

    [JsonPropertyName("subjects")]
    public string[] Subjects { get; set; } = [];

    [JsonPropertyName("guests")]
    public string[]? Guests { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLinkDto>? Services { get; set; }
}
