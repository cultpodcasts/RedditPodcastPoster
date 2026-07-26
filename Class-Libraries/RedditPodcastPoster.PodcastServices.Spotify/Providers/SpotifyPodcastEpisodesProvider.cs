using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

public class SpotifyPodcastEpisodesProvider(
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifyQueryPaginator spotifyQueryPaginator,
    ISpotifySearchResultFinder searchResultFinder,
    ILogger<SpotifyPodcastEpisodesProvider> logger
) : ISpotifyPodcastEpisodesProvider, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<string, PodcastEpisodesResult> _cache = new();

    public void ClearPassCache()
    {
        _cache.Clear();
    }

    public async Task<PodcastEpisodesResult> GetAllEpisodes(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext, string market)
    {
        var expensiveQueryFound = (bool?)null;
        EpisodeFetchResults[]? episodes = null;
        if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
        {
            var spotifyPodcastId = new SpotifyPodcastId(request.PodcastSpotifyId);
            var podcastEpisodes = await GetEpisodes(new GetEpisodesRequest(spotifyPodcastId, market), indexingContext);
            return podcastEpisodes;
        }

        if (!indexingContext.SkipPodcastDiscovery && !string.IsNullOrWhiteSpace(request.PodcastName))
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.PodcastName) {Market = market};
            var simpleShows = await spotifyClientWrapper.GetSimpleShows(searchRequest, indexingContext);
            if (simpleShows.Any())
            {
                var matchingPodcasts = searchResultFinder.FindMatchingPodcasts(request.PodcastName, simpleShows);
                var showEpisodesRequest = new ShowEpisodesRequest {Market = market};
                if (indexingContext.ReleasedSince.HasValue)
                {
                    // Spotify max page size — Limit=1 forces one HTTP call per episode and can hang for minutes
                    showEpisodesRequest.Limit = 50;
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
                    var skipUnboundedPagination =
                        indexingContext.SkipExpensiveSpotifyQueries &&
                        request.HasExpensiveSpotifyEpisodesQuery &&
                        !indexingContext.ReleasedSince.HasValue;

                    if (skipUnboundedPagination)
                    {
                        logger.LogInformation(
                            "{nameofGetAllEpisodes} - Skipping pagination of query results as {nameofSkipExpensiveSpotifyQueries} is set.",
                            nameof(GetAllEpisodes), nameof(indexingContext.SkipExpensiveSpotifyQueries));
                    }
                    else
                    {
                        if (indexingContext.SkipExpensiveSpotifyQueries &&
                            request.HasExpensiveSpotifyEpisodesQuery &&
                            indexingContext.ReleasedSince.HasValue)
                        {
                            logger.LogInformation(
                                "{nameofGetAllEpisodes} - Expensive Spotify query flagged with ReleasedSince; running bounded date-scoped pagination.",
                                nameof(GetAllEpisodes));
                        }

                        var paginateEpisodeResponse =
                            await spotifyQueryPaginator.PaginateEpisodes(
                                paging.Episodes,
                                WithSpotifyCatalogueFetchReleasedSince(indexingContext));
                        var result = paginateEpisodeResponse.Episodes.GroupBy(x => x.Id).Select(x => x.First());
                        allEpisodes.Add(result.ToList());
                        expensiveQueryFound = MergeExpensiveQueryFound(
                            expensiveQueryFound,
                            paginateEpisodeResponse.ExpensiveQueryFound);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "Null paged-list of episodes found for spotify-show-id '{pagingSpotifyPodcastId}'.",
                        paging.SpotifyPodcastId);
                }
            }

            if (allEpisodes.Any())
            {
                return new PodcastEpisodesResult(
                    TakeFreeEpisodes(
                        allEpisodes
                            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                            .Where(x => x != null && x.Any())
                            .SelectMany(x => x)
                            .GroupBy(x => x.Id)
                            .Select(x => x.First()),
                        market),
                    expensiveQueryFound);
            }
        }

        return new PodcastEpisodesResult([], expensiveQueryFound);
    }

    public async Task<PodcastEpisodesResult> GetEpisodes(
        GetEpisodesRequest request,
        IndexingContext indexingContext)
    {
        var fetchContext = WithSpotifyCatalogueFetchReleasedSince(indexingContext);
        var cacheKey = GetCacheKey(request.SpotifyPodcastId.PodcastId, indexingContext.ReleasedSince);
        if (_cache.TryGetValue(cacheKey, out var episodes))
        {
            return episodes;
        }

        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{nameofGetEpisodes}' as '{nameofSkipSpotifyUrlResolving}' is set. Podcast-Id:'{requestSpotifyPodcastIdPodcastId}'.",
                nameof(GetEpisodes), nameof(indexingContext.SkipSpotifyUrlResolving),
                request.SpotifyPodcastId.PodcastId);
            return new PodcastEpisodesResult(new List<SimpleEpisode>());
        }

        var showEpisodesRequest = new ShowEpisodesRequest {Market = market};

        if (indexingContext.ReleasedSince.HasValue)
        {
            // Expensive catalogues are oldest-first. Their first response supplies Total/Limit for
            // AscendingEpisodePaginator's end jump, so use Spotify's maximum page size to minimise
            // any backward walk through the ReleasedSince window.
            showEpisodesRequest.Limit = request.HasExpensiveSpotifyEpisodesQuery ? 50 : 5;
        }

        var pagedEpisodes =
            await spotifyClientWrapper.GetShowEpisodes(request.SpotifyPodcastId.PodcastId, showEpisodesRequest,
                indexingContext);

        if (indexingContext.SkipExpensiveSpotifyQueries &&
            request.HasExpensiveSpotifyEpisodesQuery &&
            !indexingContext.ReleasedSince.HasValue)
        {
            logger.LogInformation(
                "{nameofGetEpisodes} - Skipping pagination of query results as {nameofSkipExpensiveSpotifyQueries} is set.",
                nameof(GetEpisodes), nameof(indexingContext.SkipExpensiveSpotifyQueries));
            return new PodcastEpisodesResult(TakeFreeEpisodes(pagedEpisodes?.Items ?? [], market));
        }

        if (indexingContext.SkipExpensiveSpotifyQueries &&
            request.HasExpensiveSpotifyEpisodesQuery &&
            indexingContext.ReleasedSince.HasValue)
        {
            // Ascending catalogues return oldest first; skipping leaves only that page and misses
            // recent episodes. Date-scoped pagination is bounded (SimpleEpisodePaginator.MaxPages).
            logger.LogInformation(
                "{nameofGetEpisodes} - Expensive Spotify query flagged with ReleasedSince; running bounded date-scoped pagination.",
                nameof(GetEpisodes));
        }

        var results = await spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, fetchContext);
        var freeResults = new PodcastEpisodesResult(
            TakeFreeEpisodes(results.Episodes, market),
            results.ExpensiveQueryFound);
        _cache[cacheKey] = freeResults;
        return freeResults;
    }

    private static IndexingContext WithSpotifyCatalogueFetchReleasedSince(IndexingContext indexingContext) =>
        indexingContext with
        {
            ReleasedSince = EpisodeReleaseTolerance.GetSpotifyCatalogueFetchReleasedSince(
                indexingContext.ReleasedSince)
        };

    private static string GetCacheKey(string podcastId, DateTime? releasedSince) =>
        releasedSince.HasValue
            ? $"{podcastId}:{releasedSince.Value.Date:yyyy-MM-dd}"
            : podcastId;

    private List<SimpleEpisode> TakeFreeEpisodes(IEnumerable<SimpleEpisode> episodes, string market)
    {
        var free = new List<SimpleEpisode>();
        foreach (var episode in episodes)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (episode == null)
            {
                continue;
            }

            if (!episode.IsSpotifyFree())
            {
                SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
                continue;
            }

            free.Add(episode);
        }

        return free;
    }

    private static bool? MergeExpensiveQueryFound(bool? accumulated, bool? next) =>
        (accumulated, next) switch
        {
            (true, _) or (_, true) => true,
            (false, _) or (_, false) => false,
            _ => null
        };
}
