using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifySearcher(
    ISpotifyClientWrapper spotifyClient,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifySearcher> logger
) : ISpotifySearcher
{
    private readonly Uri _spotifyEpisodeBase = new("https://open.spotify.com/episode/");

    public async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        logger.LogInformation($"{nameof(Search)}: query: '{query}'.");
        var results = await spotifyClient.FindEpisodes(
            new SearchRequest(SearchRequest.Types.Episode, query) {Market = Market.CountryCode},
            indexingContext);
        if (results != null)
        {
            var queryRegexPattern = $@"\b{query}\b";
            var termRegex = new Regex(queryRegexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var allResults = await spotifyClient.PaginateAll(results, response => response.Episodes, indexingContext);
            var recentResults =
                allResults?
                    .Where(x => x.GetReleaseDate() >= indexingContext.ReleasedSince &&
                                (termRegex.IsMatch(x.Name) || termRegex.IsMatch(x.Description))) ??
                Enumerable.Empty<SimpleEpisode>();

            if (recentResults.Any())
            {
                var fullShows =
                    await spotifyClient.GetSeveral(
                        new EpisodesRequest(recentResults.Select(x => x.Id).ToArray()) {Market = Market.CountryCode},
                        indexingContext);

                var episodeResults = fullShows?.Episodes.Select(ToEpisodeResult) ?? Enumerable.Empty<EpisodeResult>();
                logger.LogInformation(
                    $"{nameof(Search)}: Found {episodeResults.Count()} items from spotify matching query '{query}'.");

                return episodeResults;
            }
        }

        logger.LogInformation($"{nameof(Search)}: Found no items from spotify matching query '{query}'.");
        return Enumerable.Empty<EpisodeResult>();
    }

    private EpisodeResult ToEpisodeResult(FullEpisode episode)
    {
        var image = episode.Images.MaxBy(x => x.Height);
        return new EpisodeResult(
            episode.Id,
            episode.GetReleaseDate(),
            htmlSanitiser.Sanitise(episode.HtmlDescription).Trim(),
            episode.Name.Trim(),
            episode.GetDuration(),
            episode.Show.Name.Trim(), DiscoverService.Spotify,
            new Uri(_spotifyEpisodeBase, episode.Id),
            episode.Show.Id,
            ImageUrl: image != null ? new Uri(image.Url) : null
        );
    }
}