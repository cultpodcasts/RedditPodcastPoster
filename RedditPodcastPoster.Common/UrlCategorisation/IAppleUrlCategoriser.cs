namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IAppleUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedAppleItem> Resolve(Uri url);
    Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria);
}