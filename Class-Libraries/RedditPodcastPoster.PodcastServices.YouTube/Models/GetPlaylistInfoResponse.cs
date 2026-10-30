namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <param name="ChannelId">The channel that owns the playlist (playlists.list snippet.channelId).</param>
/// <param name="ChannelTitle">The owning channel's title (snippet.channelTitle).</param>
public record GetPlaylistInfoResponse(
    string Title,
    string Description,
    string? ChannelId = null,
    string? ChannelTitle = null);
