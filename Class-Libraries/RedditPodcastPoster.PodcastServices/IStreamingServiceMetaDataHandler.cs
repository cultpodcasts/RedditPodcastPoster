using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices;

public interface IStreamingServiceMetaDataHandler
{
    Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url);
}
