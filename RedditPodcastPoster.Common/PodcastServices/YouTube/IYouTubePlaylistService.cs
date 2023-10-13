using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubePlaylistService
{
    public const int MaxSearchResults = 5;

    Task<IList<PlaylistItem>?> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId, IndexingContext indexingContext);
}