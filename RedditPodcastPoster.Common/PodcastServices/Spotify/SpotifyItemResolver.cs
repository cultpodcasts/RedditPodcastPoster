using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyItemResolver : ISpotifyItemResolver
{
    private const string Market = "GB";

    private readonly ISpotifyClient _spotifyClient;
    private readonly ILogger<SpotifyItemResolver> _logger;
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
            var episodeRequest = new EpisodeRequest() { Market = Market };
            fullEpisode = await _spotifyClient.Episodes.Get(request.EpisodeSpotifyId, episodeRequest);
        }

        if (fullEpisode == null)
        {
            (string, Paging<SimpleEpisode>)[]? episodes = null;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var showRequest = new ShowRequest {Market = Market};
                var fullShow = await _spotifyClient.Shows.Get(request.PodcastSpotifyId, showRequest);
                if (fullShow != null)
                {
                    episodes = new[] {(request.PodcastSpotifyId, fullShow.Episodes)};
                }
            }
            else
            {
                var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.PodcastName){Market = Market};
                var podcastSearchResponse = await _spotifyClient.Search.Item(searchRequest);
                if (podcastSearchResponse != null)
                {
                    var podcasts = podcastSearchResponse.Shows.Items;
                    var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);

                    var showEpisodesRequest = new ShowEpisodesRequest { Market = Market };
                    if (indexingContext.ReleasedSince.HasValue)
                    {
                        showEpisodesRequest.Limit = 1;
                    }

                    var episodesFetches = matchingPodcasts.Select(async x =>
                        await _spotifyClient.Shows.GetEpisodes(x.Id, showEpisodesRequest).ContinueWith(y =>
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
                        await PaginateEpisodes(paging.Item2, indexingContext);
                    allEpisodes.Add(simpleEpisodes);
                }

                var matchingEpisode =
                    _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, request.Released, allEpisodes);
                if (matchingEpisode != null)
                {
                    var showRequest = new EpisodeRequest {Market = Market};
                    fullEpisode = await _spotifyClient.Episodes.Get(matchingEpisode.Id, showRequest);
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
            var showRequest = new ShowRequest {Market = Market};
            matchingFullShow = await _spotifyClient.Shows.Get(request.PodcastId, showRequest);
        }

        if (matchingFullShow == null)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.Name);
            var podcasts = await _spotifyClient.Search.Item(searchRequest);
            if (podcasts != null)
            {
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
                if (request.Episodes.Any())
                {
                    foreach (var candidatePodcast in matchingPodcasts)
                    {
                        var showEpisodesRequest = new ShowEpisodesRequest { Market = Market };
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }
                        var pagedEpisodes =
                            await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id, showEpisodesRequest);
                        if (pagedEpisodes != null)
                        {
                            var allEpisodes = await PaginateEpisodes(pagedEpisodes, indexingContext);
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

        var showEpisodesRequest = new ShowEpisodesRequest { Market = Market };
        //if (indexingContext.ReleasedSince.HasValue)
        //{
        //    showEpisodesRequest.Limit = 1;
        //}

        var pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(request.PodcastId, showEpisodesRequest);

        if (pagedEpisodes != null)
        {
            return await PaginateEpisodes(pagedEpisodes, indexingContext);
        }

        return null;
    }

    private async Task<IList<SimpleEpisode>?> PaginateEpisodes(
        IPaginatable<SimpleEpisode> pagedEpisodes,
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
            var fetch = await _spotifyClient.PaginateAll(pagedEpisodes);
            episodes = fetch.ToList();
        }
        else
        {
            while (pagedEpisodes.Items!.Last().GetReleaseDate() >= indexingContext.ReleasedSince)
            {
                var batchEpisodes = await _spotifyClient.Paginate(pagedEpisodes).ToListAsync();
                episodes.AddRange(batchEpisodes);
            }
        }

        return episodes;
    }
}