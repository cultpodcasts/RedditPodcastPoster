using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultEnricher
{
    EnrichedEpisodeResult Enrich(EpisodeResult episodeResult, Podcast[] podcasts);
}
