using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.ModelTransformer.Models;

public class OldPodcast
{
    [JsonPropertyName("_id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(3)]
    public string Name { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(4)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(5)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(6)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(7)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePublicationDelay")]
    [JsonPropertyOrder(8)]
    public string YouTubePublishingDelayTimeSpan { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(9)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(10)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(11)]
    public List<OldEpisode> Episodes { get; set; } = new();

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(12)]
    public string FileKey { get; set; } = "";

    public Podcast FromName(string name)
    {
        return new Podcast {Name = name};
    }
}