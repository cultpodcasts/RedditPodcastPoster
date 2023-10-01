using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 5;

    Task<IList<SearchResult>?> GetLatestChannelVideos(YouTubeChannelId channelId, IndexingContext indexingContext);
    Task FindChannel(string channelName, IndexingContext indexingContext);
    Task<IList<Video>?> GetVideoDetails(IEnumerable<string> videoIds, IndexingContext options);
    Task<IList<PlaylistItem>?> GetPlaylist(YouTubePlaylistId playlistId, IndexingContext indexingContext);
    Task<Channel?> GetChannel(YouTubeChannelId channelId, IndexingContext indexingContext);
}