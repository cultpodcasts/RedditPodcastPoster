using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyItemResolver : ISpotifyItemResolver
{
    public const string Market = "GB";
    private readonly ILogger<SpotifyItemResolver> _logger;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyItemResolver(
        ISpotifyClient spotifyClient,
        ISpotifySearcher spotifySearcher,
        ILogger<SpotifyItemResolver> logger)
    {
        _spotifyClient = spotifyClient;
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
            try
            {
                fullEpisode = await _spotifyClient.Episodes.Get(request.EpisodeSpotifyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
                return null;
            }
        }

        if (fullEpisode == null)
        {
            Paging<SimpleEpisode>[] episodes;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                FullShow fullShow;
                try
                {
                    fullShow = await _spotifyClient.Shows.Get(request.PodcastSpotifyId,
                        new ShowRequest {Market = Market});
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                    return null;
                }

                episodes = new[] {fullShow.Episodes};
            }
            else
            {
                SearchResponse podcastSearchResponse;
                try
                {
                    podcastSearchResponse = await _spotifyClient.Search.Item(
                        new SearchRequest(SearchRequest.Types.Show, request.PodcastName)
                            {Market = Market});
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                    return null;
                }

                var podcasts = podcastSearchResponse.Shows.Items;
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);
                var episodesFetches = matchingPodcasts.Select(async x =>
                    await _spotifyClient.Shows.GetEpisodes(x.Id,
                        new ShowEpisodesRequest {Market = Market}));
                episodes = await Task.WhenAll(episodesFetches);
            }

            IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
            foreach (var paging in episodes)
            {
                var simpleEpisodes = await PaginateEpisodes(paging, indexingContext);
                allEpisodes.Add(simpleEpisodes);
            }

            var matchingEpisode =
                _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, request.Released, allEpisodes);
            if (matchingEpisode != null)
            {
                try
                {
                    fullEpisode = await _spotifyClient.Episodes.Get(matchingEpisode.Id,
                        new EpisodeRequest {Market = Market});
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                    return null;
                }
            }
        }

        return fullEpisode;
    }


    public async Task<SpotifyPodcastWrapper?> FindPodcast(FindSpotifyPodcastRequest request, IndexingContext indexingContext)
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
            try
            {
                matchingFullShow = await _spotifyClient.Shows.Get(request.PodcastId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
                return null;
            }
        }

        if (matchingFullShow == null)
        {
            SearchResponse podcasts;
            try
            {
                podcasts = await _spotifyClient.Search.Item(new SearchRequest(SearchRequest.Types.Show, request.Name)
                    {Market = Market});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
                return null;
            }

            var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
            if (request.Episodes.Any())
            {
                foreach (var candidatePodcast in matchingPodcasts)
                {
                    Paging<SimpleEpisode> pagedEpisodes;
                    try
                    {
                        pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id,
                            new ShowEpisodesRequest {Market = Market});
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed using {nameof(_spotifyClient)}.");
                        indexingContext.SkipSpotifyUrlResolving = true;
                        return null;
                    }

                    var allEpisodes = await PaginateEpisodes(pagedEpisodes, indexingContext);

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

            matchingSimpleShow ??= matchingPodcasts.MaxBy(x => Levenshtein.CalculateSimilarity(request.Name, x.Name));
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow);
    }


    public async Task<IEnumerable<SimpleEpisode>> GetEpisodes(
        SpotifyPodcastId request,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(GetEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastId}'.");
            return null;
        }

        IPaginatable<SimpleEpisode> pagedEpisodes;
        try
        {
            pagedEpisodes =
                await _spotifyClient.Shows.GetEpisodes(request.PodcastId,
                    new ShowEpisodesRequest {Market = Market});
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed using {nameof(_spotifyClient)}.");
            indexingContext.SkipSpotifyUrlResolving = true;
            return Enumerable.Empty<SimpleEpisode>();
        }

        return await PaginateEpisodes(pagedEpisodes, indexingContext);
    }

    private async Task<IList<SimpleEpisode>> PaginateEpisodes(
        IPaginatable<SimpleEpisode> pagedEpisodes,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            _logger.LogInformation(
                $"Skipping '{nameof(PaginateEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set.");
            return new List<SimpleEpisode>();
        }

        IList<SimpleEpisode> episodes;

        if (indexingContext.ReleasedSince == null)
        {
            IList<SimpleEpisode> fetch;
            try
            {
                fetch = await _spotifyClient.PaginateAll(pagedEpisodes);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed using {nameof(_spotifyClient)}.");
                indexingContext.SkipSpotifyUrlResolving = true;
                return new List<SimpleEpisode>();
            }

            episodes = fetch.ToList();
        }
        else
        {
            while (pagedEpisodes.Items!.Last().GetReleaseDate() > indexingContext.ReleasedSince)
            {
                try
                {
                    await _spotifyClient.Paginate(pagedEpisodes).ToListAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed using {nameof(_spotifyClient)}.");
                    indexingContext.SkipSpotifyUrlResolving = true;
                    return new List<SimpleEpisode>();
                }
            }

            episodes = pagedEpisodes.Items!;
        }

        return episodes;
    }
}