using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 20;

    Task<IList<SearchResult>> GetLatestChannelVideos(GetLatestYouTubeChannelVideosRequest request);
    Task FindChannel(string channelName);
    Task<IList<Video>> GetVideoDetails(IEnumerable<string> videoIds);
    Task<IList<PlaylistItem>> GetPlaylist(GetYouTubePlaylistItems request);
    Task<Channel?> GetChannel(string channelId);
}