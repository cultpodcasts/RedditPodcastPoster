using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IStreamingServiceMetaDataHandler
{
    Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url);
}