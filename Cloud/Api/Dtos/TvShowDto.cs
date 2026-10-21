using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Curator GET for a TV show: the shared publisher fields plus canonical ids.</summary>
public class TvShowDto : PublisherFieldsDto
{
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
