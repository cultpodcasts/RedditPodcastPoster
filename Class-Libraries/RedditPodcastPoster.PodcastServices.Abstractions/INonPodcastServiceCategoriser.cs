using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface INonPodcastServiceCategoriser
{
    Task<ResolvedNonPodcastServiceItem?> Resolve(Podcast? podcast, Uri url, IndexingContext indexingContext);
}
