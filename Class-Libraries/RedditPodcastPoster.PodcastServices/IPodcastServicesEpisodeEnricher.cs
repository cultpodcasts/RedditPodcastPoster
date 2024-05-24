using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastServicesEpisodeEnricher
{
    Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext);
}