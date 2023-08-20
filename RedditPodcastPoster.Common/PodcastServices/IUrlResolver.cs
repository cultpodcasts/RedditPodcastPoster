using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public interface IUrlResolver
{
    Task<bool> ResolveEpisodeUrls(
        Podcast podcast,
        IList<Episode> newEpisodes,
        DateTime? publishedSince,
        bool skipYouTubeUrlResolving);
}