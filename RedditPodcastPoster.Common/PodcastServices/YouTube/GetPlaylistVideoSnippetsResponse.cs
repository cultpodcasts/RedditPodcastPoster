using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public record GetPlaylistVideoSnippetsResponse(IList<PlaylistItem>? Result, bool IsExpensiveQuery = false);