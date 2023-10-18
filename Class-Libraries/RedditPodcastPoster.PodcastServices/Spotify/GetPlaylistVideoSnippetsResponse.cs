using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public record GetPlaylistVideoSnippetsResponse(IList<PlaylistItem>? Result, bool IsExpensiveQuery = false);