using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices;

public interface IStreamingServiceMetaDataHandler
{
    Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url);
}