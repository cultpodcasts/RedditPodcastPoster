using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Search;

public class SpotifySearcher(
    ISpotifyClientWrapper spotifyClient,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifySearcher> logger
) : ISpotifySearcher
{
    private readonly Uri _spotifyEpisodeBase = new("https://open.spotify.com/episode/");

    public async Task<IList<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        if (indexingContext.ReleasedSince.HasValue)
        {
            indexingContext = indexingContext with
            {
                ReleasedSince = indexingContext.ReleasedSince!.Value.ToUniversalTime().Floor(TimeSpan.FromDays(1))
            };
        }

        logger.LogInformation("{SpotifySearcherName}.{SearchName}: query: '{Query}'.", nameof(SpotifySearcher), nameof(Search), query);
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
                    .Where(x =>
                        x != null &&
                        x.GetReleaseDate() >= indexingContext.ReleasedSince &&
                        (termRegex.IsMatch(x.Name) || termRegex.IsMatch(x.Description))) ?? [];

            if (recentResults.Any())
            {
                var episodeIds = recentResults.Select(x => x.Id).ToArray();
                var fullShows =
                    await spotifyClient.GetSeveral(
                        new EpisodesRequest(episodeIds) {Market = Market.CountryCode},
                        indexingContext);

                if (fullShows == null)
                {
                    logger.LogError(
                        "{SearchName}: GetSeveral returned no response hydrating {EpisodeIdCount} episode-ids for query '{Query}'; discovery results for this query will be empty. Episode-ids: '{EpisodeIds}'.",
                        nameof(Search), episodeIds.Length, query, string.Join(",", episodeIds));
                }

                var episodeResults = fullShows?.Episodes.Select(ToEpisodeResult) ?? Enumerable.Empty<EpisodeResult>();
                logger.LogInformation(
                    "{SearchName}: Found {Count} items from spotify matching query '{Query}'.", nameof(Search), episodeResults.Count(x => x.Released >= indexingContext.ReleasedSince), query);

                return episodeResults.ToList();
            }
        }

        logger.LogInformation("{SearchName}: Found no items from spotify matching query '{Query}'.", nameof(Search), query);
        return new List<EpisodeResult>();
    }

    private EpisodeResult ToEpisodeResult(FullEpisode episode)
    {
        var image = episode.GetBestImageUrl();
        var episodeResult = new EpisodeResult(
            episode.Id,
            episode.GetReleaseDate(),
            htmlSanitiser.Sanitise(episode.HtmlDescription).Trim(),
            episode.Name.Trim(),
            episode.GetDuration(),
            episode.Show.Name.Trim(),
            episode.Show.Description,
            DiscoverService.Spotify,
            imageUrl: image
        );
        episodeResult.Urls.Spotify = new Uri(_spotifyEpisodeBase, episode.Id);
        return episodeResult;
    }
}