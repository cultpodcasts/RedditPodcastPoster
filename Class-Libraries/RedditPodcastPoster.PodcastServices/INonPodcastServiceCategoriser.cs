using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface INonPodcastServiceCategoriser
{
    Task<ResolvedNonPodcastServiceItem?> Resolve(Uri url, IndexingContext indexingContext);
}