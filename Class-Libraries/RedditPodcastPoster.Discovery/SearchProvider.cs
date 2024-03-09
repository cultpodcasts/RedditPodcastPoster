using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.ListenNotes;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.Discovery;

public class SearchProvider(
    ISpotifySearcher spotifySearcher,
    IListenNotesSearcher listenNotesSearcher,
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
            switch (config.DiscoveryService)
            {
                case DiscoveryService.ListenNotes:
                    serviceResults = await listenNotesSearcher.Search(config.Term, indexingContext);
                    break;
                case DiscoveryService.Spotify:
                    serviceResults = await spotifySearcher.Search(config.Term, indexingContext);
                    break;
                case DiscoveryService.YouTube:
                    serviceResults = await youTubeSearcher.Search(config.Term, indexingContext);
                    break;
                default:
                    logger.LogError($"Unhandled {nameof(DiscoveryService)}");
                    break;
            }

            results.AddRange(serviceResults);
        }

        return results
            .GroupBy(x => x.EpisodeName)
            .Select(x => x.FirstOrDefault(y => y.Url != null) ?? x.First())
            .OrderBy(x => x.Released);
    }
}