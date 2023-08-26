using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class Podcast
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string Name { get; set; }

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(4)]
    public string Publisher { get; set; }

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(5)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(6)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(7)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(8)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePublicationDelay")]
    [JsonPropertyOrder(9)]
    public string YouTubePublishingDelayTimeSpan { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(10)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(11)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(12)]
    public List<Episode> Episodes { get; set; } = new();

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(13)]
    public string FileKey { get; set; }

    public Podcast FromName(string name)
    {
        return new Podcast {Name = name};
    }
}