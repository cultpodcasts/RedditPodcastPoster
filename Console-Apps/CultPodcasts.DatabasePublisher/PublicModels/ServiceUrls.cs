using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public class PublicServiceUrls
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(1)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(2)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(3)]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("internetArchive")]
    [JsonPropertyOrder(4)]
    public Uri? InternetArchive { get; set; }

    [JsonPropertyName("bbc")]
    [JsonPropertyOrder(4)]
    public Uri? BBC { get; set; }
}