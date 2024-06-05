using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultsEnricher
{
    IAsyncEnumerable<EnrichedEpisodeResult> EnrichWithPodcastDetails(IEnumerable<EpisodeResult> episodeResults);
}