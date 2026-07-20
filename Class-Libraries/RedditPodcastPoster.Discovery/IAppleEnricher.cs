using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery;

public interface IAppleEnricher
{
    Task Enrich(IList<EpisodeResult> results, IndexingContext indexingContext);
}
