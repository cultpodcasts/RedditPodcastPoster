using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IPodcastServicesEpisodeEnricher
{
    Task<EnrichmentResults> EnrichEpisodes(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        IList<Episode> newEpisodes,
        IndexingContext indexingContext);
}
