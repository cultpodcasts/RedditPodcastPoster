using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.Discovery;

public class SearchProvider(
    ISpotifySearcher spotifySearcher,
    IListenNotesSearcher listenNotesSearcher,
    ISpotifyEnricher spotifyEnricher,
    IAppleEnricher appleEnricher,
    IYouTubeSearcher youTubeSearcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchProvider> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISearchProvider
{
    public async Task<IEnumerable<EpisodeResult>> GetEpisodes(
        IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig)
    {
        var results = new List<EpisodeResult>();
        foreach (var config in discoveryConfig.ServiceConfigs)
        {
            IList<EpisodeResult> serviceResults;
            switch (config.DiscoverService)
            {
                case DiscoverService.ListenNotes:
                    serviceResults = await listenNotesSearcher.Search(config.Term, indexingContext);
                    if (discoveryConfig.EnrichFromSpotify)
                    {
                        await spotifyEnricher.Enrich(serviceResults, indexingContext);
                    }

                    if (discoveryConfig.EnrichFromApple)
                    {
                        await appleEnricher.Enrich(serviceResults, indexingContext);
                    }

                    break;
                case DiscoverService.Spotify:
                    serviceResults = await spotifySearcher.Search(config.Term, indexingContext);
                    if (discoveryConfig.EnrichFromApple)
                    {
                        await appleEnricher.Enrich(serviceResults, indexingContext);
                    }

                    break;
                case DiscoverService.YouTube:
                    serviceResults = await youTubeSearcher.Search(config.Term, indexingContext);
                    break;
                default:
                    logger.LogError($"Unhandled {nameof(DiscoverService)}");
                    serviceResults = new List<EpisodeResult>();
                    break;
            }

            results.AddRange(serviceResults);
        }

        var items = results
            .Where(x => x.Released >= indexingContext.ReleasedSince)
            .GroupBy(x => x.EpisodeName)
            .Select(Coalesce)
            .OrderBy(x => x.Released);

        string[] logItems =
        [
            $"total-items: '{items.Count()}'",
            $"spotify-items '{items.Count(x => x.DiscoverService == PodcastServices.Abstractions.DiscoverService.Spotify)}'",
            $"youtube-items: '{items.Count(x => x.DiscoverService == PodcastServices.Abstractions.DiscoverService.YouTube)}'",
            $"listen-notes-items: '{items.Count(x => x.DiscoverService == PodcastServices.Abstractions.DiscoverService.ListenNotes)}'",
            $"spotify-enriched: '{items.Count(x => x.EnrichedFrom == PodcastServices.Abstractions.EnrichmentService.Spotify)}'",
            $"apple-enriched: '{items.Count(x => x.EnrichedFrom == PodcastServices.Abstractions.EnrichmentService.Apple)}'"
        ];

        logger.LogInformation($"{string.Join(", ", logItems)}.");
        return items;
    }

    private EpisodeResult Coalesce(IGrouping<string, EpisodeResult> items, int index)
    {
        var first = items.First();
        var appleTime = false;
        foreach (var subsequent in items.Skip(1))
        {
            first.Urls.Apple ??= subsequent.Urls.Apple;
            first.Urls.Spotify ??= subsequent.Urls.Spotify;
            first.Urls.YouTube ??= subsequent.Urls.YouTube;
            if (!appleTime)
            {
                first.EnrichedFrom ??= subsequent.EnrichedFrom;
            }

            if (first is {DiscoverService: PodcastServices.Abstractions.DiscoverService.Spotify, EnrichedFrom: null} &&
                subsequent.DiscoverService != PodcastServices.Abstractions.DiscoverService.Spotify)
            {
                if (!appleTime)
                {
                    first.Released = subsequent.Released;
                    if (subsequent.EnrichedFrom == PodcastServices.Abstractions.EnrichmentService.Apple)
                    {
                        appleTime = true;
                        first.EnrichedFrom = PodcastServices.Abstractions.EnrichmentService.Apple;
                    }
                }
            }
        }

        return first;
    }
}