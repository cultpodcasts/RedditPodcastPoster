namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IYouTubeUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedYouTubeItem> Resolve(Uri url);
    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria);
}