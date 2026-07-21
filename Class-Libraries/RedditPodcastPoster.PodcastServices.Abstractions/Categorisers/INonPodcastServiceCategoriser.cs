using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;

public interface INonPodcastServiceCategoriser
{
    Task<ResolvedNonPodcastServiceItem?> Resolve(Podcast? podcast, Uri url, IndexingContext indexingContext);
}
