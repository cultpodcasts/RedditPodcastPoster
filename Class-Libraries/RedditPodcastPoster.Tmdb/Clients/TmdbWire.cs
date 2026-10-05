using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Tmdb.Clients;

internal sealed class TmdbSearchResponse
{
    public List<TmdbListedTitleDto>? Results { get; set; }
}

internal sealed class TmdbListedTitleDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }
}

internal sealed class TmdbDetailsDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

    [JsonPropertyName("external_ids")]
    public TmdbExternalIdsDto? ExternalIds { get; set; }
}

internal sealed class TmdbExternalIdsDto
{
    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdb_id")]
    public long? TvdbId { get; set; }
}
