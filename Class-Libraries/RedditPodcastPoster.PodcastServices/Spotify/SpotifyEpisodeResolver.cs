using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeResolver : ISpotifyEpisodeResolver
{
    private const string Market = "GB";
    private readonly ILogger<SpotifyEpisodeResolver> _logger;
    private readonly ISpotifyClientWrapper _spotifyClientWrapper;
    private readonly ISpotifyQueryPaginator _spotifyQueryPaginator;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyEpisodeResolver(
        ISpotifyClientWrapper spotifyClientWrapper,
        ISpotifySearcher spotifySearcher,
        ISpotifyQueryPaginator spotifyQueryPaginator,
        ILogger<SpotifyEpisodeResolver> logger)
    {
        _spotifyClientWrapper = spotifyClientWrapper;
        _spotifySearcher = spotifySearcher;
        _spotifyQueryPaginator = spotifyQueryPaginator;
        _logger = logger;
    }

    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext)
    {
        var market = request.Market ?? Market;
        var expensiveQueryFound = false;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindEpisode)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastSpotifyId}', Podcast-Name:'{request.PodcastName}', Episode-Id:'{request.EpisodeSpotifyId}', Episode-Name:'{request.EpisodeTitle}'.");
            return new FindEpisodeResponse(null);
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest
                {Market = market };
            fullEpisode =
                await _spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
        }

        if (fullEpisode == null)
        {
            (string, Paging<SimpleEpisode>?)[]? episodes = null;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var showRequest = new ShowRequest {Market = market};
                var fullShow =
                    await _spotifyClientWrapper.GetFullShow(request.PodcastSpotifyId, showRequest, indexingContext);
                if (fullShow != null)
                {
                    episodes = new (string, Paging<SimpleEpisode>?)[] {(request.PodcastSpotifyId, fullShow.Episodes)};
                }
            }
            else
            {
                if (!indexingContext.SkipPodcastDiscovery)
                {
                    var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.PodcastName)
                        {Market = market};
                    var podcastSearchResponse =
                        await _spotifyClientWrapper.GetSearchResponse(searchRequest, indexingContext);
                    if (podcastSearchResponse != null)
                    {
                        var podcasts = podcastSearchResponse.Shows.Items;
                        var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);

                        var showEpisodesRequest = new ShowEpisodesRequest {Market = market};
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }

                        var episodesFetches = matchingPodcasts.Select(async x =>
                            await _spotifyClientWrapper.GetShowEpisodes(x.Id, showEpisodesRequest, indexingContext)
                                .ContinueWith(y =>
                                    new ValueTuple<string, Paging<SimpleEpisode>?>(x.Id, y.Result)));
                        episodes = await Task.WhenAll(episodesFetches);
                    }
                }
            }

            if (episodes != null)
            {
                IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
                foreach (var paging in episodes)
                {
                    if (paging.Item2 != null)
                    {
                        if (indexingContext.SkipExpensiveQueries && request.HasExpensiveSpotifyEpisodesQuery)
                        {
                            _logger.LogInformation(
                                $"{nameof(FindEpisode)} - Skipping pagination of query results as {nameof(indexingContext.SkipExpensiveQueries)} is set.");
                        }
                        else
                        {
                            var paginateEpisodeResponse =
                                await _spotifyQueryPaginator.PaginateEpisodes(paging.Item2, indexingContext);
                            allEpisodes.Add(paginateEpisodeResponse.Results);
                            if (paginateEpisodeResponse.IsExpensiveQuery)
                            {
                                expensiveQueryFound = true;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Null paged-list of episodes found for spotify-show-id '{paging.Item1}'.");
                    }
                }

                var matchingEpisode =
                    _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, indexingContext.ReleasedSince,
                        allEpisodes);
                if (matchingEpisode != null)
                {
                    var showRequest = new EpisodeRequest {Market = market};
                    fullEpisode =
                        await _spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
                }
            }
        }

        return new FindEpisodeResponse(fullEpisode, expensiveQueryFound);
    }

    public async Task<PaginateEpisodesResponse> GetEpisodes(
        GetEpisodesRequest request,
        IndexingContext indexingContext)
    {
        var market = request.Market ?? Market;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.SpotifyPodcastId.PodcastId}'.");
            return new PaginateEpisodesResponse(new List<SimpleEpisode>());
        }

        var showEpisodesRequest = new ShowEpisodesRequest {Market = market};

        if (indexingContext.ReleasedSince.HasValue)
        {
            showEpisodesRequest.Limit = 5;
        }

        var pagedEpisodes =
            await _spotifyClientWrapper.GetShowEpisodes(request.SpotifyPodcastId.PodcastId, showEpisodesRequest,
                indexingContext);

        if (indexingContext.SkipExpensiveQueries && request.HasExpensiveSpotifyEpisodesQuery)
        {
            _logger.LogInformation(
                $"{nameof(GetEpisodes)} - Skipping pagination of query results as {nameof(indexingContext.SkipExpensiveQueries)} is set.");
            return new PaginateEpisodesResponse(pagedEpisodes?.Items ?? new List<SimpleEpisode>());
        }

        return await _spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
    }
}