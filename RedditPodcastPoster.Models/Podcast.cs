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
    public ModelType ModelType { get; set; } = ModelType.Podcast;

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string Name { get; set; } = "";

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(4)]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(5)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("indexAllEpisodes")]
    [JsonPropertyOrder(6)]
    public bool IndexAllEpisodes { get; set; } = false;

    [JsonPropertyName("primaryPostService")]
    [JsonPropertyOrder(7)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(20)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(21)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(22)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePublicationDelay")]
    [JsonPropertyOrder(40)]
    public string YouTubePublishingDelayTimeSpan { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(60)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(61)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(62)]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(80)]
    public List<Episode> Episodes { get; set; } = new();

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(100)]
    public string FileKey { get; set; } = "";

    public Podcast FromName(string name)
    {
        return new Podcast {Name = name};
    }
}