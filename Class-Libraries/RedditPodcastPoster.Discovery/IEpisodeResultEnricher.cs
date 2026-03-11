using RedditPodcastPoster.PodcastServices.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultEnricher
{
    EnrichedEpisodeResult Enrich(EpisodeResult episodeResult, Podcast[] podcasts);
}