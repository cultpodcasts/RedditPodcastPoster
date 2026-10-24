namespace Api.Models;

/// <summary>Publisher fields plus the service ids (news organisation, TV show). Null = unchanged.</summary>
public abstract class ServiceIdsChange : PublisherFieldsChange
{
    public string? YouTubeChannelId { get; set; }
    public string? YouTubePlaylistId { get; set; }
    public string? SpotifyId { get; set; }
    public long? AppleId { get; set; }
    public bool? NullAppleId { get; set; }
    public bool? IndexAllEpisodes { get; set; }
    public string? TitleRegex { get; set; }
    public string? DescriptionRegex { get; set; }
    public string? EpisodeMatchRegex { get; set; }
    public string? EpisodeIncludeTitleRegex { get; set; }

    public bool ChangesServiceIds =>
        YouTubeChannelId != null || YouTubePlaylistId != null || SpotifyId != null || AppleId != null ||
        NullAppleId == true || IndexAllEpisodes != null || TitleRegex != null || DescriptionRegex != null ||
        EpisodeMatchRegex != null || EpisodeIncludeTitleRegex != null;
}
