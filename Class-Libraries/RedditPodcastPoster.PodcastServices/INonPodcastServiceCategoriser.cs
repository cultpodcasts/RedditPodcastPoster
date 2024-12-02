using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface INonPodcastServiceCategoriser
{
    Task<ResolvedNonPodcastServiceItem?> Resolve(Podcast? podcast, Uri url, IndexingContext indexingContext);
}