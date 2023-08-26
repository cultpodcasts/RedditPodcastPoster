using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public interface IPodcastServicesEpisodeEnricher
{
    Task EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        DateTime? publishedSince,
        bool skipYouTubeUrlResolving);
}