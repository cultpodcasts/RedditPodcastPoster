using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public sealed class PublicTvShow(Guid id) : PublicCatalogueEntity(id, ModelType.TvShow)
{
    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonPropertyName("imdbId")]
    [JsonPropertyOrder(5)]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tvdbId")]
    [JsonPropertyOrder(6)]
    public long? TvdbId { get; set; }

    [JsonPropertyName("tmdbId")]
    [JsonPropertyOrder(7)]
    public int? TmdbId { get; set; }

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(11)]
    public List<PublicTvShowEpisode> Episodes { get; set; } = new();
}
