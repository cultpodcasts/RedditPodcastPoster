using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public sealed class PublicPodcast : CosmosSelector
{
    public PublicPodcast(Guid id)
    {
        Id = id;
        ModelType = ModelType.Podcast;
    }

    [JsonIgnore]
    public override ModelType ModelType { get; set; }

    [JsonIgnore]
    public override string FileKey { get; set; } = "";

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string? Name { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(5)]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(6)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(7)]
    public string? YouTubeChannelId { get; set; }

    [JsonPropertyName("youTubePlaylistId")]
    [JsonPropertyOrder(8)]
    public string? YouTubePlaylistId { get; set; }

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(11)]
    public List<PublicEpisode> Episodes { get; set; } = new();
}