using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Dtos;

/// <summary>Curator GET shared by news organisations and TV shows: the publisher fields plus the service ids from <c>indexing</c>.</summary>
public abstract class ServiceIdsDto : PublisherFieldsDto
{
    [JsonPropertyName("youTubeChannelId")]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistId")]
    public string YouTubePlaylistId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistIdHistory")]
    public YouTubePlaylistIdHistoryEntry[]? YouTubePlaylistIdHistory { get; set; }

    [JsonPropertyName("spotifyId")]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    /// <summary>From <c>indexing</c>: auto-index every episode, or only titles matching the include regex.</summary>
    [JsonPropertyName("indexAllEpisodes")]
    public bool IndexAllEpisodes { get; set; }

    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string EpisodeIncludeTitleRegex { get; set; } = "";
}
