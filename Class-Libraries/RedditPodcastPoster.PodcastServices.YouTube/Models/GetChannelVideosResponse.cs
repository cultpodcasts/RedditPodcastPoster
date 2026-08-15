using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetChannelVideosResponse(
    Google.Apis.YouTube.v3.Data.Channel? Channel,
    IList<PlaylistItem>? PlaylistItems,
    YouTubePlaylistFetchFailure? Failure = null);
