using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using SpotifyAPI.Web;

namespace Discover;

public class DiscoveryProcessor
{
    private const string? Market = "GB";
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
        var results = await Search("\"Cults\"", indexingContext);
        results = results.Concat(await Search("\"Cult\"", indexingContext));
        results = results.Concat(await Search("\"Scientology\"", indexingContext));
        results = results.Concat(await Search("\"NXIVM\"", indexingContext));
        results = results.Concat(await Search("\"FLDS\"", indexingContext));

        foreach (var episode in results.GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.GetReleaseDate()))
        {
            var min = Math.Min(episode.Description.Length, 200);
            Console.WriteLine($"https://open.spotify.com/episode/{episode.Id}");
            Console.WriteLine(episode.Name);
            Console.WriteLine(episode.Show.Name);
            Console.WriteLine(episode.Description[..min]);
            Console.WriteLine(episode.GetReleaseDate().ToString("g"));
            Console.WriteLine();
        }
    }

    private async Task<IEnumerable<FullEpisode>> Search(string query, IndexingContext indexingContext)
    {
        var results = await _spotifyClient.FindEpisodes(
            new SearchRequest(SearchRequest.Types.Episode, query) {Market = Market},
            indexingContext);
        var allResults = await _spotifyClient.PaginateAll(results, response => response.Episodes, indexingContext);
        var recentResults = allResults?.Where(x => x.GetReleaseDate() >= indexingContext.ReleasedSince) ??
                            Enumerable.Empty<SimpleEpisode>();

        if (recentResults.Any())
        {
            var fullShows =
                await _spotifyClient.GetSeveral(
                    new EpisodesRequest(recentResults.Select(x => x.Id).ToArray()) {Market = Market}, indexingContext);

            return fullShows?.Episodes ?? Enumerable.Empty<FullEpisode>();
        }

        return Enumerable.Empty<FullEpisode>();
    }
}