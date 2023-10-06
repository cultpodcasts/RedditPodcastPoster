using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 5;

    Task<IList<SearchResult>?> GetLatestChannelVideoSnippets(YouTubeChannelId channelId,
        IndexingContext indexingContext);

    Task FindChannel(string channelName, IndexingContext indexingContext);
    Task<IList<Video>?> GetVideoContentDetails(IEnumerable<string> videoIds, IndexingContext options);
    Task<IList<PlaylistItem>?> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId, IndexingContext indexingContext);

    Task<Channel?> GetChannelContentDetails(YouTubeChannelId channelId, IndexingContext indexingContext,
        bool withSnippets = false, bool withContentOwnerDetails = false);
}