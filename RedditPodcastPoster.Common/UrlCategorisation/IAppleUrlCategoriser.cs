using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IAppleUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedAppleItem> Resolve(List<Podcast> podcasts, Uri url);
    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast);
}