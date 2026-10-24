using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// PATCH body shared by news organisations and TV shows: the publisher fields plus the service ids, stored under
/// the parent's <c>indexing</c> block (<c>CatalogueIndexingSource</c>, #1050). The wire names and rules are the
/// podcast edit API's (<see cref="PodcastChangeRequest"/>). Null means unchanged and <c>""</c> clears. URL
/// submission uses these ids to attach a submitted episode to the outlet or show, instead of a podcast.
/// </summary>
public abstract class ServiceIdsChangeRequest : PublisherFieldsChangeRequest
{
    [JsonPropertyName("youTubeChannelId")]
    public string? YouTubeChannelId { get; set; }

    [JsonPropertyName("youTubePlaylistId")]
    public string? YouTubePlaylistId { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("nullAppleId")]
    public bool? NullAppleId { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool? IndexAllEpisodes { get; set; }

    [JsonPropertyName("titleRegex")]
    public string? TitleRegex { get; set; }

    [JsonPropertyName("descriptionRegex")]
    public string? DescriptionRegex { get; set; }

    [JsonPropertyName("episodeMatchRegex")]
    public string? EpisodeMatchRegex { get; set; }

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string? EpisodeIncludeTitleRegex { get; set; }
}
