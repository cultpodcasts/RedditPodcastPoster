using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.Podcast)]
public class Podcast : CosmosSelector
{
    public Podcast() : base(ModelType.Podcast)
    {
    }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }


    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = "";

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(21)]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(30)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("indexAllEpisodes")]
    [JsonPropertyOrder(31)]
    public bool IndexAllEpisodes { get; set; } = false;

    [JsonPropertyName("releaseAuthority")]
    [JsonPropertyOrder(40)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonPropertyOrder(41)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(50)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(51)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(52)]
    public string YouTubeChannelId { get; set; } = "";

    /// <summary>
    ///     This only works for Spotify-channels with YouTube where this is a side-podcast in a YouTube-playlist
    /// </summary>
    [JsonPropertyName("youTubePlaylistId")]
    [JsonPropertyOrder(53)]
    public string YouTubePlaylistId { get; set; }

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(53)]
    public string TwitterHandle { get; set; }

    [JsonPropertyName("youTubePublicationDelay")]
    [JsonPropertyOrder(60)]
    public string YouTubePublishingDelayTimeSpan { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(70)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(71)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(72)]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    [JsonPropertyOrder(73)]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

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