using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultsEnricher
{
    IAsyncEnumerable<EnrichedEpisodeResult> EnrichWithPodcastDetails(IEnumerable<EpisodeResult> episodeResults);
}
