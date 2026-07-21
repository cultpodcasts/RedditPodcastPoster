using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Categorisers;

public interface IAppleUrlCategoriser
{
    Task<ResolvedAppleItem> Resolve(Podcast? podcasts, IEnumerable<Episode> episodes, Uri url, IndexingContext indexingContext);

    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}
