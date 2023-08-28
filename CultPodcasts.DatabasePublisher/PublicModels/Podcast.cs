using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public class PublicPodcast
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string Name { get; set; } = "";

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(5)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(6)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(7)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(11)]
    public List<PublicEpisode> Episodes { get; set; } = new();
}