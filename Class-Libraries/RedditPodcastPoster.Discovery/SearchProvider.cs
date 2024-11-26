using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Taddy;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.Discovery;

public class SearchProvider(
    ISpotifySearcher spotifySearcher,
    IListenNotesSearcher listenNotesSearcher,
    ITaddySearcher taddySearcher,
    ISpotifyEnricher spotifyEnricher,
    IAppleEnricher appleEnricher,
    IYouTubeSearcher youTubeSearcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchProvider> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISearchProvider
{
    public async Task<IEnumerable<EpisodeResult>> GetEpisodes(
        DiscoveryConfig discoveryConfig,
        IndexingContext indexingContext)
    {
        var results = new List<EpisodeResult>();
        foreach (var config in discoveryConfig.ServiceConfigs)
        {
            IList<EpisodeResult> serviceResults;
            switch (config.DiscoverService)
            {
                case DiscoverService.Taddy:
                    var taddyIndexingContent = indexingContext with
                    {
                        ReleasedSince = discoveryConfig.Since - (discoveryConfig.TaddyOffset ?? TimeSpan.Zero)
                    };
                    serviceResults = await taddySearcher.Search(config.Term, taddyIndexingContent);
                    if (discoveryConfig.EnrichFromSpotify)
                    {
                        await spotifyEnricher.Enrich(serviceResults, taddyIndexingContent);
                    }

                    if (discoveryConfig.EnrichFromApple)
                    {
                        await appleEnricher.Enrich(serviceResults, taddyIndexingContent);
                    }

                    indexingContext = taddyIndexingContent with
                    {
                        ReleasedSince = indexingContext.ReleasedSince
                    };
                    break;

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
            .Where(x =>
                (x.DiscoverServices.FirstOrDefault() == DiscoverService.Taddy &&
                 x.Released >= indexingContext.ReleasedSince!.Value.Subtract(TaddyParameters.IndexingDelay)) ||
                x.Released >= indexingContext.ReleasedSince)
            .GroupBy(x => x.EpisodeName)
            .Select(Coalesce)
            .OrderBy(x => x.Released);

        string[] logItems =
        [
            $"total-items: '{items.Count()}'",
            $"spotify-items '{items.Count(x => x.DiscoverServices.Contains(DiscoverService.Spotify))}'",
            $"youtube-items: '{items.Count(x => x.DiscoverServices.Contains(DiscoverService.YouTube))}'",
            $"listen-notes-items: '{items.Count(x => x.DiscoverServices.Contains(DiscoverService.ListenNotes))}'",
            $"taddy-items: '{items.Count(x => x.DiscoverServices.Contains(DiscoverService.Taddy))}'",
            $"spotify-enriched-url: '{items.Count(x => x.EnrichedUrlFromSpotify)}'",
            $"apple-enriched-release: '{items.Count(x => x.EnrichedTimeFromApple)}'"
        ];

        logger.LogInformation($"{string.Join(", ", logItems)}.");
        return items;
    }

    private EpisodeResult Coalesce(IGrouping<string, EpisodeResult> items, int index)
    {
        var first = items.First();
        foreach (var subsequent in items.Skip(1))
        {
            first.Urls.Apple ??= subsequent.Urls.Apple;
            first.Urls.Spotify ??= subsequent.Urls.Spotify;
            first.Urls.YouTube ??= subsequent.Urls.YouTube;
        }

        var youTube =
            items.FirstOrDefault(x =>
                x.DiscoverServices.Contains(DiscoverService.YouTube));
        if (youTube != null)
        {
            first.Released = youTube.Released;
            first.ViewCount = youTube.ViewCount;
            first.MemberCount = youTube.MemberCount;
        }

        if (string.IsNullOrWhiteSpace(first.ShowDescription))
        {
            first.ShowDescription =
                items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ShowDescription))?.ShowDescription ??
                string.Empty;
        }

        var apple = items.FirstOrDefault(x => x.EnrichedTimeFromApple);
        if (apple != null)
        {
            first.Released = apple.Released;
            first.EnrichedTimeFromApple = true;
        }

        if (items.Any(x => x.EnrichedUrlFromSpotify))
        {
            first.EnrichedUrlFromSpotify = true;
        }

        first.DiscoverServices = items.SelectMany(x => x.DiscoverServices).Distinct().ToArray();

        return first;
    }
}