using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Curator GET for a TV-show episode: the shared playable fields plus its show and canonical ids.</summary>
public class TvShowEpisodeDto : PlayableFieldsDto
{
    [JsonPropertyName("tvShowId")]
    public Guid TvShowId { get; set; }

    [JsonPropertyName("tvShowName")]
    public string? TvShowName { get; set; }

    [JsonPropertyName("imdb")]
    public Uri? Imdb { get; set; }

    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdb")]
    public Uri? Tvdb { get; set; }

    [JsonPropertyName("tvdbId")]
    public long? TvdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }
}
