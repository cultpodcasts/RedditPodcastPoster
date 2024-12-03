using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IStreamingServiceMetaDataHandler
{
    Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        Uri url);
}