using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeSearchService
{
    public const int MaxSearchResults = 20;

    Task<IList<SearchResult>> GetLatestChannelVideos(Podcast podcast, DateTime? publishedSince);
    Task FindChannel(string channelName);
    Task<IList<Video>> GetVideoDetails(IEnumerable<string> videoIds);
    Task<IList<PlaylistItem>> GetPlaylist(string playlistId);
}