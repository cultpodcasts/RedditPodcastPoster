using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultAdapter
{
    Task<DiscoveryResult> ToDiscoveryResult(EpisodeResult episode);
}