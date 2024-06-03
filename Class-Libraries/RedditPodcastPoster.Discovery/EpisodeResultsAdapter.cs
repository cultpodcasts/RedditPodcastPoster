using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using DiscoverService = RedditPodcastPoster.PodcastServices.Abstractions.DiscoverService;

namespace RedditPodcastPoster.Discovery;

public class EpisodeResultsAdapter(
    IPodcastRepository podcastRepository,
    IIgnoreTermsProvider ignoreTermsProvider,
    IEpisodeResultAdapter episodeResultAdapter,
    ILogger<EpisodeResultsAdapter> logger) : IEpisodeResultsAdapter
{
    public async IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IEnumerable<EpisodeResult> episodeResults)
    {
        logger.LogInformation($"{nameof(ToDiscoveryResults)} initiated.");
        var podcastIds = podcastRepository.GetAllBy(podcast =>
                podcast.IndexAllEpisodes || podcast.EpisodeIncludeTitleRegex != string.Empty,
            x => new
            {
                x.YouTubeChannelId,
                SpotifyShowId = x.SpotifyId
            });
        var indexedYouTubeChannelIds = await podcastIds.Select(x => x.YouTubeChannelId).Distinct().ToListAsync();
        var indexedSpotifyChannelIds = await podcastIds.Select(x => x.SpotifyShowId).Distinct().ToListAsync();


        var ignoreTerms = ignoreTermsProvider.GetIgnoreTerms();

        foreach (var episode in episodeResults)
        {
            if ((episode.DiscoverService == DiscoverService.YouTube &&
                 indexedYouTubeChannelIds.Contains(episode.ServicePodcastId!)) ||
                (episode.DiscoverService == DiscoverService.Spotify &&
                 indexedSpotifyChannelIds.Contains(episode.ServicePodcastId!)))
            {
                continue;
            }

            var ignored = false;
            foreach (var ignoreTerm in ignoreTerms)
            {
                if (episode.Description.ToLower().Contains(ignoreTerm) ||
                    episode.EpisodeName.ToLower().Contains(ignoreTerm))
                {
                    ignored = true;
                }
            }

            if (!ignored)
            {
                var discoveryResult = await episodeResultAdapter.ToDiscoveryResult(episode);

                yield return discoveryResult;
            }
        }
    }
}