using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 5;

    Task<IList<SearchResult>?> GetLatestChannelVideos(YouTubeChannelId channelId, IndexOptions indexOptions);
    Task FindChannel(string channelName, IndexOptions indexOptions);
    Task<IList<Video>?> GetVideoDetails(IEnumerable<string> videoIds, IndexOptions options);
    Task<IList<PlaylistItem>?> GetPlaylist(YouTubePlaylistId playlistId, IndexOptions indexOptions);
    Task<Channel?> GetChannel(YouTubeChannelId channelId, IndexOptions indexOptions);
}