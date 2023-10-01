using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IAppleUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedAppleItem> Resolve(List<Podcast> podcasts, Uri url, IndexingContext indexingContext);

    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext);
}