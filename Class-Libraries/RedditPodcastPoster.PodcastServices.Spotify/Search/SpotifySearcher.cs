﻿using System.Text.RegularExpressions;
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

        logger.LogInformation($"{nameof(SpotifySearcher)}.{nameof(Search)}: query: '{query}'.");
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
                var fullShows =
                    await spotifyClient.GetSeveral(
                        new EpisodesRequest(recentResults.Select(x => x.Id).ToArray()) {Market = Market.CountryCode},
                        indexingContext);

                var episodeResults = fullShows?.Episodes.Select(ToEpisodeResult) ?? Enumerable.Empty<EpisodeResult>();
                logger.LogInformation(
                    $"{nameof(Search)}: Found {episodeResults.Count(x => x.Released >= indexingContext.ReleasedSince)} items from spotify matching query '{query}'.");

                return episodeResults.ToList();
            }
        }

        logger.LogInformation($"{nameof(Search)}: Found no items from spotify matching query '{query}'.");
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