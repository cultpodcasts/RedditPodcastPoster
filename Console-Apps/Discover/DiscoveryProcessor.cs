using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using SpotifyAPI.Web;

namespace Discover;

public class DiscoveryProcessor
{
    private readonly ILogger<DiscoveryProcessor> _logger;
    private readonly ISpotifyClientWrapper _spotifyClient;

    public DiscoveryProcessor(
        ISpotifyClientWrapper spotifyClient,
        ILogger<DiscoveryProcessor> logger)
    {
        _spotifyClient = spotifyClient;
        _logger = logger;
    }

    public async Task Process(DiscoveryRequest request)
    {
        var indexingContext = new IndexingContext(
            DateTimeHelper.DaysAgo(request.NumberOfDays),
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);
        await Search("\"Cults\"", indexingContext);
        //await Search("\"Cult\"", indexingContext);
    }

    private async Task Search(string query, IndexingContext indexingContext)
    {
        var results = await _spotifyClient.FindEpisodes(
            new SearchRequest(SearchRequest.Types.Episode, query) {Market = "GB"},
            indexingContext);
        if (results != null)
        {
            var allResults = await _spotifyClient.PaginateAll(results, response => response.Episodes, indexingContext);
            var recentResults = allResults.Where(x => x.GetReleaseDate() >= indexingContext.ReleasedSince);
            foreach (var simpleEpisode in recentResults)
            {
                _logger.LogInformation($"{simpleEpisode.Id}: '{simpleEpisode.Name}'");
                _logger.LogInformation($"{simpleEpisode.Description[..200]}");
            }
        }
    }
}