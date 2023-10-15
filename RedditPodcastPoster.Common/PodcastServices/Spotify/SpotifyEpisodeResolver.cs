using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

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

    public async Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindEpisode)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastSpotifyId}', Podcast-Name:'{request.PodcastName}', Episode-Id:'{request.EpisodeSpotifyId}', Episode-Name:'{request.EpisodeTitle}'.");
            return null;
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest {Market = Market};
            fullEpisode =
                await _spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
        }

        if (fullEpisode == null)
        {
            (string, Paging<SimpleEpisode>?)[]? episodes = null;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var showRequest = new ShowRequest {Market = Market};
                var fullShow =
                    await _spotifyClientWrapper.GetFullShow(request.PodcastSpotifyId, showRequest, indexingContext);
                if (fullShow != null)
                {
                    episodes = new (string, Paging<SimpleEpisode>?)[] {(request.PodcastSpotifyId, fullShow.Episodes)};
                }
            }
            else
            {
                var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.PodcastName) {Market = Market};
                var podcastSearchResponse =
                    await _spotifyClientWrapper.GetSearchResponse(searchRequest, indexingContext);
                if (podcastSearchResponse != null)
                {
                    var podcasts = podcastSearchResponse.Shows.Items;
                    var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);

                    var showEpisodesRequest = new ShowEpisodesRequest {Market = Market};
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

            if (episodes != null)
            {
                IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
                foreach (var paging in episodes)
                {
                    if (paging.Item2 != null)
                    {
                        var simpleEpisodes =
                            await _spotifyQueryPaginator.PaginateEpisodes(paging.Item2, indexingContext);
                        allEpisodes.Add(simpleEpisodes);
                    }
                }

                var matchingEpisode =
                    _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, indexingContext.ReleasedSince,
                        allEpisodes);
                if (matchingEpisode != null)
                {
                    var showRequest = new EpisodeRequest {Market = Market};
                    fullEpisode =
                        await _spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
                }
            }
        }

        return fullEpisode;
    }

    public async Task<IEnumerable<SimpleEpisode>?> GetEpisodes(
        SpotifyPodcastId request,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastId}'.");
            return null;
        }

        var showEpisodesRequest = new ShowEpisodesRequest {Market = Market};

        if (indexingContext.ReleasedSince.HasValue)
        {
            showEpisodesRequest.Limit = 5;
        }

        var pagedEpisodes =
            await _spotifyClientWrapper.GetShowEpisodes(request.PodcastId, showEpisodesRequest, indexingContext);

        return await _spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
    }
}