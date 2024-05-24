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
            var serviceResults = Enumerable.Empty<EpisodeResult>();
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
                    break;
            }

            results.AddRange(serviceResults);
        }

        var items = results
            .Where(x => x.Released >= indexingContext.ReleasedSince)
            .GroupBy(x => x.EpisodeName)
            .Select(x => x.FirstOrDefault(y => y.Url != null) ?? x.First())
            .OrderBy(x => x.Released);

        logger.LogInformation(
            $"total-items: '{items.Count()}', spotify-items '{items.Count(x => x.DiscoverService == DiscoverService.Spotify)}, youtube-items: '{items.Count(x => x.DiscoverService == DiscoverService.YouTube)}', listen-notes-items: '{items.Count(x => x.DiscoverService == DiscoverService.ListenNotes)}'.");
        return items;
    }
}