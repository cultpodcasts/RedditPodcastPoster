using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public interface IAppleUrlCategoriser
{
    Task<ResolvedAppleItem> Resolve(Podcast? podcasts, IEnumerable<Episode> episodes, Uri url, IndexingContext indexingContext);

    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}