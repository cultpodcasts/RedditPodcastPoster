using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public interface IPodcastServicesEpisodeEnricher
{
    Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext);
}