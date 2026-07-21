using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Enrichers;

public interface IEpisodeResultsEnricher
{
    IAsyncEnumerable<EnrichedEpisodeResult> EnrichWithPodcastDetails(IEnumerable<EpisodeResult> episodeResults);
}
