using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyPodcastResolver : ISpotifyPodcastResolver
{
    private const string Market = "GB";
    private readonly ILogger<SpotifyPodcastResolver> _logger;
    private readonly ISpotifyClientWrapper _spotifyClientWrapper;
    private readonly ISpotifyQueryPaginator _spotifyQueryPaginator;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyPodcastResolver(
        ISpotifyClientWrapper spotifyClientWrapper,
        ISpotifySearcher spotifySearcher,
        ISpotifyQueryPaginator spotifyQueryPaginator,
        ILogger<SpotifyPodcastResolver> logger)
    {
        _spotifyClientWrapper = spotifyClientWrapper;
        _spotifySearcher = spotifySearcher;
        _spotifyQueryPaginator = spotifyQueryPaginator;
        _logger = logger;
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
            matchingFullShow = await _spotifyClientWrapper.GetFullShow(request.PodcastId, showRequest, indexingContext);
        }

        if (matchingFullShow == null)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.Name);
            var podcasts = await _spotifyClientWrapper.GetSearchResponse(searchRequest, indexingContext);
            if (podcasts != null)
            {
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
                if (request.Episodes.Any())
                {
                    foreach (var candidatePodcast in matchingPodcasts)
                    {
                        var showEpisodesRequest = new ShowEpisodesRequest {Market = Market};
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }

                        var pagedEpisodes =
                            await _spotifyClientWrapper.GetShowEpisodes(candidatePodcast.Id, showEpisodesRequest,
                                indexingContext);
                        if (pagedEpisodes != null)
                        {
                            var allEpisodes =
                                await _spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
                            if (allEpisodes.Any())
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
}