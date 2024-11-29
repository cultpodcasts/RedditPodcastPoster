using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistVideoSnippetsResponse(IList<PlaylistItem>? Result, bool IsExpensiveQuery = false);