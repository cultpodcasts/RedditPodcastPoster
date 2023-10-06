using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyItemResolver : ISpotifyItemResolver
{
    private readonly ICachedSpotifyClient _cachedSpotifyClient;
    private readonly ILogger<SpotifyItemResolver> _logger;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyItemResolver(
        ISpotifyClient spotifyClient,
        ICachedSpotifyClient cachedSpotifyClient,
        ISpotifySearcher spotifySearcher,
        ILogger<SpotifyItemResolver> logger)
    {
        _cachedSpotifyClient = cachedSpotifyClient;
        _spotifySearcher = spotifySearcher;
        _logger = logger;
    }

    public async Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request, IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindEpisode)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastSpotifyId}', Podcast-Name:'{request.PodcastName}', Episode-Id:'{request.EpisodeSpotifyId}', Episode-Name:'{request.EpisodeTitle}', Released:{request.Released:R}.");
            return null;
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            fullEpisode = await _cachedSpotifyClient.Episodes.Get(request.EpisodeSpotifyId, indexingContext);
        }

        if (fullEpisode == null)
        {
            (string, Paging<SimpleEpisode>)[]? episodes = null;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var fullShow = await _cachedSpotifyClient.Shows.Get(request.PodcastSpotifyId, indexingContext);
                if (fullShow != null)
                {
                    episodes = new[] {(request.PodcastSpotifyId, fullShow.Episodes)};
                }
            }
            else
            {
                var podcastSearchResponse = await _cachedSpotifyClient.Search.Item(
                    new SearchRequest(SearchRequest.Types.Show, request.PodcastName), indexingContext);
                if (podcastSearchResponse != null)
                {
                    var podcasts = podcastSearchResponse.Shows.Items;
                    var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);
                    var episodesFetches = matchingPodcasts.Select(async x =>
                        await _cachedSpotifyClient.Shows.GetEpisodes(x.Id, indexingContext).ContinueWith(y =>
                            new ValueTuple<string, Paging<SimpleEpisode>>(x.Id, y.Result)));
                    episodes = await Task.WhenAll(episodesFetches);
                }
            }

            if (episodes != null)
            {
                IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
                foreach (var paging in episodes)
                {
                    var simpleEpisodes =
                        await PaginateEpisodes(paging.Item2, $"findepisode-{paging.Item1}", indexingContext);
                    allEpisodes.Add(simpleEpisodes);
                }

                var matchingEpisode =
                    _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, request.Released, allEpisodes);
                if (matchingEpisode != null)
                {
                    fullEpisode = await _cachedSpotifyClient.Episodes.Get(matchingEpisode.Id, indexingContext);
                }
            }
        }

        return fullEpisode;
    }

    public async Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(FindPodcast)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastId}', Podcast-Name:'{request.Name}'.");
            return null;
        }

        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        if (!string.IsNullOrWhiteSpace(request.PodcastId))
        {
            matchingFullShow = await _cachedSpotifyClient.Shows.Get(request.PodcastId, indexingContext);
        }

        if (matchingFullShow == null)
        {
            var podcasts = await _cachedSpotifyClient.Search.Item(
                new SearchRequest(SearchRequest.Types.Show, request.Name), indexingContext);
            if (podcasts != null)
            {
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
                if (request.Episodes.Any())
                {
                    foreach (var candidatePodcast in matchingPodcasts)
                    {
                        var pagedEpisodes =
                            await _cachedSpotifyClient.Shows.GetEpisodes(candidatePodcast.Id, indexingContext);
                        if (pagedEpisodes != null)
                        {
                            var allEpisodes = await PaginateEpisodes(pagedEpisodes,
                                $"findpodcast-{candidatePodcast.Id}", indexingContext);
                            if (allEpisodes != null)
                            {
                                var mostRecentEpisode = request.Episodes.OrderByDescending(x => x.Release).First();
                                var matchingEpisode =
                                    _spotifySearcher.FindMatchingEpisode(
                                        mostRecentEpisode.Title,
                                        mostRecentEpisode.Release,
                                        new[] {allEpisodes});
                                if (request.Episodes
                                    .Select(x => x.Url?.ToString())
                                    .Contains(matchingEpisode!.ExternalUrls.FirstOrDefault().Value))
                                {
                                    matchingSimpleShow = candidatePodcast;
                                    break;
                                }
                            }
                        }
                    }
                }

                matchingSimpleShow ??=
                    matchingPodcasts.MaxBy(x => Levenshtein.CalculateSimilarity(request.Name, x.Name));
            }
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow);
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

        var pagedEpisodes = await _cachedSpotifyClient.Shows.GetEpisodes(request.PodcastId, indexingContext);

        if (pagedEpisodes != null)
        {
            return await PaginateEpisodes(pagedEpisodes, $"getpodcastepisodes-{request.PodcastId}", indexingContext);
        }

        return null;
    }

    private async Task<IList<SimpleEpisode>?> PaginateEpisodes(
        IPaginatable<SimpleEpisode> pagedEpisodes,
        string cacheKey,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(PaginateEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set.");
            return new List<SimpleEpisode>();
        }

        List<SimpleEpisode> episodes = pagedEpisodes.Items.ToList();

        if (indexingContext.ReleasedSince == null)
        {
            var fetch = await _cachedSpotifyClient.PaginateAll(pagedEpisodes, cacheKey, indexingContext);
            episodes = fetch.ToList();
        }
        else
        {
            var page = 1;
            while (pagedEpisodes.Items!.Last().GetReleaseDate() >= indexingContext.ReleasedSince)
            {
                var batchEpisodes =
                    await _cachedSpotifyClient.Paginate(pagedEpisodes, $"{cacheKey}-{page++}", indexingContext);
                episodes.AddRange(batchEpisodes);
            }
        }

        return episodes;
    }
}