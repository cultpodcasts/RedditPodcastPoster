using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public interface IStreamingServiceMetaDataHandler
{
    Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url);
}