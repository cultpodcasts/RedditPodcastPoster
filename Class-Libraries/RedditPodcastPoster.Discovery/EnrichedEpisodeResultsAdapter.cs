using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public class EnrichedEpisodeResultsAdapter(
    IIgnoreTermsProvider ignoreTermsProvider,
    IEnrichedEpisodeResultAdapter enrichedEpisodeResultAdapter,
    ILogger<EnrichedEpisodeResultsAdapter> logger) : IEnrichedEpisodeResultsAdapter
{
    public async IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IEnumerable<EnrichedEpisodeResult> episodeResults)
    {
        logger.LogInformation($"{nameof(ToDiscoveryResults)} initiated.");

        var ignoreTerms = ignoreTermsProvider.GetIgnoreTerms();

        foreach (var episode in episodeResults)
        {
            if (episode.PodcastResults.Any(x => x.IndexAllEpisodes))
            {
                logger.LogInformation(
                    "Eliminated episode '{EpisodeResultId}' as found in indexed-podcast with podcast-id: '{Join}'.", episode.EpisodeResult.Id, string.Join(",", episode.PodcastResults.Select(x => x.PodcastId)));
                continue;
            }

            var ignored = false;
            foreach (var ignoreTerm in ignoreTerms)
            {
                if (episode.EpisodeResult.Description.ToLower().Contains(ignoreTerm) ||
                    episode.EpisodeResult.EpisodeName.ToLower().Contains(ignoreTerm))
                {
                    ignored = true;
                }
            }

            if (!ignored)
            {
                var discoveryResult = await enrichedEpisodeResultAdapter.ToDiscoveryResult(episode);

                yield return discoveryResult;
            }
        }
    }
}