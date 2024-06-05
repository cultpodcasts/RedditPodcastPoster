using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultEnricher
{
    EnrichedEpisodeResult Enrich(EpisodeResult episodeResult, Podcast[] podcasts);
}