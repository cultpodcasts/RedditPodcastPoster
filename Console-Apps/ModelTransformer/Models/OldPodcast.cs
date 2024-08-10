using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace ModelTransformer.Models;

[CosmosSelector(ModelType.Podcast)]
public sealed class OldPodcast : CosmosSelector
{
    public OldPodcast(Guid id)
    {
        Id = id;
        ModelType = ModelType.Podcast;
    }

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

    [JsonPropertyName("useYouTubeAsReleaseAuthority")]
    [JsonPropertyOrder(7)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonPropertyOrder(8)]
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

    [JsonPropertyName("youTubePublicationOffset")]
    [JsonPropertyOrder(30)]
    public long? YouTubePublicationOffset { get; set; }

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(60)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(61)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(62)]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    [JsonPropertyOrder(63)]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(80)]
    public List<OldEpisode> Episodes { get; set; } = new();
}