using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastServicesEpisodeEnricher
{
    Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext);
}