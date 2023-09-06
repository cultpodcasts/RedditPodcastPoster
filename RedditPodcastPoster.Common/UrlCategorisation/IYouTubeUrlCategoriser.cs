using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public interface IYouTubeUrlCategoriser : IPodcastServiceUrlResolver
{
    Task<ResolvedYouTubeItem> Resolve(List<Podcast> podcasts, Uri url);
    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast);
}