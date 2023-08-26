using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeItemResolver
{
    Task<SearchResult?> FindEpisode(Podcast podcast, Episode episode, DateTime? publishedSince);
}