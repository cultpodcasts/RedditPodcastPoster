using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public sealed class PublicTvShowEpisode : PublicPlayable
{
    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(10)]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdbId")]
    [JsonPropertyOrder(11)]
    public long? TvdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(12)]
    public int? TmdbId { get; set; }
}
