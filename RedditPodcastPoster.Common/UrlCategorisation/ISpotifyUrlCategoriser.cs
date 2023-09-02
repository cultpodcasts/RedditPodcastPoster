namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface ISpotifyUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedSpotifyItem> Resolve(Uri url);

    Task<ResolvedSpotifyItem?> Resolve(PodcastServiceSearchCriteria criteria);
}