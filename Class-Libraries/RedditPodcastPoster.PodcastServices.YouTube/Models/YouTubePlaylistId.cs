namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record YouTubePlaylistId(
    string PlaylistId,
    YouTubePlaylistIdSource Source = YouTubePlaylistIdSource.Unknown,
    string? SourceIdentifier = null);