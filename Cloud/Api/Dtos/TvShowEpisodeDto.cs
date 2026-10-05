using System.Text.Json.Serialization;

namespace Api.Dtos;

public class TvShowEpisodeDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("tvShowId")]
    public Guid TvShowId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

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
