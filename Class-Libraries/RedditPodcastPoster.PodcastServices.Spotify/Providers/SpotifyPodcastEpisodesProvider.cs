using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public class SpotifyPodcastEpisodesProvider(
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifyQueryPaginator spotifyQueryPaginator,
    ISearchResultFinder searchResultFinder,
    ILogger<SpotifyPodcastEpisodesProvider> logger
) : ISpotifyPodcastEpisodesProvider
{
    private readonly ConcurrentDictionary<string, PodcastEpisodesResult> _cache = new();

    public void Flush()
    {
        _cache.Clear();
    }

    public async Task<PodcastEpisodesResult> GetAllEpisodes(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext, string market)
    {
        var expensiveQueryFound = false;
        EpisodeFetchResults[]? episodes = null;
        if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
        {
            var spotifyPodcastId = new SpotifyPodcastId(request.PodcastSpotifyId);
            var podcastEpisodes = await GetEpisodes(new GetEpisodesRequest(spotifyPodcastId, market), indexingContext);
            return podcastEpisodes;
        }

        if (!indexingContext.SkipPodcastDiscovery && !string.IsNullOrWhiteSpace(request.PodcastName))
        {
            var searchRequest = new SearchRequest(
                    SearchRequest.Types.Show,
                    request.PodcastName)
                {Market = market};
            var simpleShows = await spotifyClientWrapper.GetSimpleShows(searchRequest, indexingContext);
            if (simpleShows.Any())
            {
                var matchingPodcasts =
                    searchResultFinder.FindMatchingPodcasts(request.PodcastName, simpleShows);

                var showEpisodesRequest = new ShowEpisodesRequest {Market = market};
                if (indexingContext.ReleasedSince.HasValue)
                {
                    showEpisodesRequest.Limit = 1;
                }

                var episodesFetches = matchingPodcasts
                    .Select(async x => await spotifyClientWrapper
                        .GetShowEpisodes(x.Id, showEpisodesRequest, indexingContext)
                        .ContinueWith(y =>
                            new EpisodeFetchResults(x.Id, y.Result)));
                episodes = await Task.WhenAll(episodesFetches);
            }
        }

        if (episodes != null)
        {
            IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
            foreach (var paging in episodes)
            {
                if (paging.Episodes != null)
                {
                    if (indexingContext.SkipExpensiveSpotifyQueries && request.HasExpensiveSpotifyEpisodesQuery)
                    {
                        logger.LogInformation(
                            $"{nameof(GetAllEpisodes)} - Skipping pagination of query results as {nameof(indexingContext.SkipExpensiveSpotifyQueries)} is set.");
                    }
                    else
                    {
                        var paginateEpisodeResponse =
                            await spotifyQueryPaginator.PaginateEpisodes(paging.Episodes, indexingContext);
                        var result = paginateEpisodeResponse.Episodes.GroupBy(x => x.Id).Select(x => x.First());
                        allEpisodes.Add(result.ToList());
                        if (paginateEpisodeResponse.ExpensiveQueryFound)
                        {
                            expensiveQueryFound = true;
                        }
                    }
                }
                else
                {
                    logger.LogWarning(
                        $"Null paged-list of episodes found for spotify-show-id '{paging.SpotifyPodcastId}'.");
                }
            }

            if (allEpisodes.Any())
            {
                return new PodcastEpisodesResult(
                    allEpisodes
                        .Where(x => x != null && x.Any())
                        .SelectMany(x => x)
                        .GroupBy(x => x.Id)
                        .FirstOrDefault() ?? Enumerable.Empty<SimpleEpisode>(),
                    expensiveQueryFound);
            }
        }

        return new PodcastEpisodesResult([], expensiveQueryFound);
    }

    public async Task<PodcastEpisodesResult> GetEpisodes(
        GetEpisodesRequest request,
        IndexingContext indexingContext)
    {
        if (_cache.TryGetValue(request.SpotifyPodcastId.PodcastId, out var episodes))
        {
            return episodes;
        }

        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.SpotifyPodcastId.PodcastId}'.");
            return new PodcastEpisodesResult(new List<SimpleEpisode>());
        }

        var showEpisodesRequest = new ShowEpisodesRequest {Market = market};

        if (indexingContext.ReleasedSince.HasValue)
        {
            showEpisodesRequest.Limit = 5;
        }

        var pagedEpisodes =
            await spotifyClientWrapper.GetShowEpisodes(request.SpotifyPodcastId.PodcastId, showEpisodesRequest,
                indexingContext);

        if (indexingContext.SkipExpensiveSpotifyQueries && request.HasExpensiveSpotifyEpisodesQuery)
        {
            logger.LogInformation(
                $"{nameof(GetEpisodes)} - Skipping pagination of query results as {nameof(indexingContext.SkipExpensiveSpotifyQueries)} is set.");
            return new PodcastEpisodesResult(pagedEpisodes?.Items ?? new List<SimpleEpisode>());
        }

        var results = await spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
        _cache[request.SpotifyPodcastId.PodcastId] = results;
        return results;
    }
}