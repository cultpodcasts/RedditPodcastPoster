using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace Discover;

public class SpotifySearcher(
    ISpotifyClientWrapper spotifyClient,
    IHtmlSanitiser htmlSanitiser,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifySearcher> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISpotifySearcher
{
    private readonly Uri _spotifyEpisodeBase = new("https://open.spotify.com/episode/");

    public async Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
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

                return fullShows?.Episodes.Select(ToEpisodeResult) ?? Enumerable.Empty<EpisodeResult>();
            }
        }

        return Enumerable.Empty<EpisodeResult>();
    }

    private EpisodeResult ToEpisodeResult(FullEpisode episode)
    {
        return new EpisodeResult(
            episode.Id,
            episode.GetReleaseDate(),
            htmlSanitiser.Sanitise(episode.HtmlDescription).Trim(),
            episode.Name.Trim(),
            episode.Show.Name.Trim(),
            new Uri(_spotifyEpisodeBase, episode.Id));
    }
}