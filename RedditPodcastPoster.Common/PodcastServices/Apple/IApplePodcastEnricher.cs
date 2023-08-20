using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IApplePodcastEnricher
{
    Task AddIdAndUrls(Podcast podcast, IEnumerable<Episode> newEpisodes);
}