using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface ISpotifyUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedSpotifyItem> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext);

    Task<ResolvedSpotifyItem?> Resolve(
        PodcastServiceSearchCriteria criteria, 
        Podcast? matchingPodcast,
        IndexingContext indexingContext);
}