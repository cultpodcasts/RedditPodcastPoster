namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubePlaylistService
{
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId,
        IndexingContext indexingContext);
}