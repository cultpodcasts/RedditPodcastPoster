using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// The show-level ids behind an episode URL: the Spotify show id, the Apple podcast id, or the YouTube
/// channel id (with the channel title, used to break ties between podcasts). All null for other services.
/// </summary>
public sealed record EpisodeUrlOwnerIds(
    string? SpotifyShowId = null,
    long? ApplePodcastId = null,
    string? YouTubeChannelId = null,
    string? YouTubeChannelTitle = null)
{
    public static readonly EpisodeUrlOwnerIds None = new();

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SpotifyShowId) && ApplePodcastId == null && string.IsNullOrWhiteSpace(YouTubeChannelId);
}

/// <summary>
/// The podcast that owns an episode URL (if any) and the ids it was matched on. When
/// <paramref name="ChannelRouteDecided"/> is true, the YouTube channel matcher already chose between the channel's
/// podcasts and its news organisation / TV show (<paramref name="Parent"/>, when it won), so the router must not
/// re-check playlist membership or look the parent up again.
/// </summary>
public sealed record EpisodeUrlOwner(
    Podcast? Podcast,
    EpisodeUrlOwnerIds Ids,
    CatalogueParentMatch? Parent = null,
    bool ChannelRouteDecided = false);
