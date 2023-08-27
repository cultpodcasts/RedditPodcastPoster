using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

public class PublicEpisode
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(3)]
    public string Title { get; init; }

    [JsonPropertyName("description")]
    [JsonPropertyOrder(4)]
    public string Description { get; init; }

    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    public DateTime Release { get; init; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    public TimeSpan Length { get; init; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(9)]
    public bool Explicit { get; init; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(10)]
    public string SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(11)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(12)]
    public string YouTubeId { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(13)]
    public List<string> Subjects { get; set; } = new();

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(14)]
    public PublicServiceUrls Urls { get; set; } = new();
}