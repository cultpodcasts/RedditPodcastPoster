using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class CachedSpotifyClient : ICachedSpotifyClient
{
    private const string Market = "GB";
    private static readonly ConcurrentDictionary<string, IList<SimpleEpisode>> Cache = new();
    private readonly ILogger<CachedSpotifyClient> _logger;
    private readonly ISpotifyClient _spotifyClient;

    public CachedSpotifyClient(
        ISpotifyClient spotifyClient,
        ILogger<CachedSpotifyClient> logger,
        ILogger<CachedShowsClient> showClientLogger,
        ILogger<CachedEpisodesClient> episodeClientLogger,
        ILogger<CachedSearchClient> episodeSearchLogger)
    {
        _spotifyClient = spotifyClient;
        _logger = logger;
        Shows = new CachedShowsClient(spotifyClient.Shows, showClientLogger);
        Episodes = new CachedEpisodesClient(spotifyClient.Episodes, episodeClientLogger);
        Search = new CachedSearchClient(spotifyClient.Search, episodeSearchLogger);
    }

    public async Task<IList<SimpleEpisode>?> PaginateAll(
        IPaginatable<SimpleEpisode> firstPage,
        string cacheKey,
        IndexingContext indexingContext,
        IPaginator? paginator = null)
    {
        IList<SimpleEpisode>? results = null;
        if (!Cache.TryGetValue(cacheKey, out results))
        {
            try
            {
                results = Cache[cacheKey] = await _spotifyClient.PaginateAll(firstPage, paginator);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
            }
        }

        return results;
    }

    public async Task<IList<SimpleEpisode>?> Paginate(
        IPaginatable<SimpleEpisode> firstPage,
        string cacheKey,
        IndexingContext indexingContext,
        IPaginator? paginator = null,
        CancellationToken cancel = default)
    {
        IList<SimpleEpisode>? results = null;
        if (!Cache.TryGetValue(cacheKey, out results))
        {
            try
            {
                results = Cache[cacheKey] =
                    await _spotifyClient.Paginate(firstPage, paginator, cancel).ToListAsync(cancel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
            }
        }

        return results!;
    }


    public CachedEpisodesClient Episodes { get; }

    public CachedShowsClient Shows { get; }

    public CachedSearchClient Search { get; }

    public class CachedShowsClient
    {
        private static readonly ConcurrentDictionary<string, FullShow> FullShowCache = new();
        private static readonly ConcurrentDictionary<string, Paging<SimpleEpisode>> PagingSimpleEpisodeCache = new();

        private readonly ILogger<CachedShowsClient> _logger;
        private readonly IShowsClient _showsClient;

        public CachedShowsClient(
            IShowsClient showsClient,
            ILogger<CachedShowsClient> logger
        )
        {
            _logger = logger;
            _showsClient = showsClient;
        }

        private async Task<FullShow?> Get(string showId, ShowRequest request, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            FullShow? fullShow = null;
            var cacheKey = GetCacheKey(showId, request);
            if (!FullShowCache.TryGetValue(cacheKey, out fullShow))
            {
                try
                {
                    fullShow = FullShowCache[cacheKey] = await _showsClient.Get(showId, request, cancel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                }
            }

            return fullShow;
        }

        public async Task<FullShow?> Get(string showId, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            FullShow? fullShow = null;
            var cacheKey = GetCacheKey(showId, nameof(Get));
            if (!FullShowCache.TryGetValue(cacheKey, out fullShow))
            {
                try
                {
                    fullShow = FullShowCache[cacheKey] =
                        await Get(showId, new ShowRequest {Market = Market}, indexingContext, cancel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                }
            }

            return fullShow;
        }

        public async Task<Paging<SimpleEpisode>?> GetEpisodes(string showId, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            var request = new ShowEpisodesRequest {Market = Market};
            Paging<SimpleEpisode>? episodes = null;
            var cacheKey = GetCacheKey(showId, nameof(GetEpisodes));
            if (!PagingSimpleEpisodeCache.TryGetValue(cacheKey, out episodes))
            {
                try
                {
                    episodes = PagingSimpleEpisodeCache[cacheKey] =
                        await _showsClient.GetEpisodes(showId, request, cancel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                }
            }

            return episodes;
        }

        private string GetCacheKey(string showId, string type)
        {
            return $"{showId}-{type}";
        }

        private string GetCacheKey(string showId, ShowRequest request)
        {
            return $"{showId}-{request.Market}";
        }
    }

    public class CachedSearchClient
    {
        private static readonly ConcurrentDictionary<string, SearchResponse> Cache = new();

        private readonly ILogger<CachedSearchClient> _logger;
        private readonly ISearchClient _searchClient;

        public CachedSearchClient(
            ISearchClient searchClient,
            ILogger<CachedSearchClient> logger
        )
        {
            _logger = logger;
            _searchClient = searchClient;
        }

        public async Task<SearchResponse?> Item(SearchRequest request, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            request.Market = Market;
            SearchResponse? searchResponse = null;
            var cacheKey = GetCacheKey(request);
            if (!Cache.TryGetValue(cacheKey, out searchResponse))
            {
                try
                {
                    searchResponse = Cache[cacheKey] = await _searchClient.Item(request, cancel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                }
            }

            return searchResponse;
        }

        private string GetCacheKey(SearchRequest request)
        {
            return $"{request.Query}-{request.Type.ToString()}-{request.Market}-{request.Limit}-{request.Offset}";
        }
    }

    public class CachedEpisodesClient
    {
        private static readonly ConcurrentDictionary<string, FullEpisode> Cache = new();

        private readonly IEpisodesClient _episodesClient;
        private readonly ILogger<CachedEpisodesClient> _logger;

        public CachedEpisodesClient(
            IEpisodesClient episodesClient,
            ILogger<CachedEpisodesClient> logger
        )
        {
            _logger = logger;
            _episodesClient = episodesClient;
        }

        public async Task<FullEpisode?> Get(string episodeId, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            return await Get(episodeId, new EpisodeRequest {Market = Market}, indexingContext, cancel);
        }

        private async Task<FullEpisode?> Get(string episodeId, EpisodeRequest request, IndexingContext indexingContext,
            CancellationToken cancel = default)
        {
            FullEpisode? fullEpisode = null;
            var cacheKey = GetCacheKey(episodeId, request);
            if (!Cache.TryGetValue(cacheKey, out fullEpisode))
            {
                try
                {
                    fullEpisode = Cache[cacheKey] = await _episodesClient.Get(episodeId, request, cancel);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                }
            }

            return fullEpisode;
        }

        private string GetCacheKey(string episodeId, EpisodeRequest request)
        {
            return $"{episodeId}-{request.Market}";
        }
    }
}