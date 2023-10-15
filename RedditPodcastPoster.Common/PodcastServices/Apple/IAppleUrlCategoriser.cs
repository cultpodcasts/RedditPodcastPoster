using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedAppleItem> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext);

    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}