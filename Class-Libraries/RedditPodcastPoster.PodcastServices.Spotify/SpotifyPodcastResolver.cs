using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyPodcastResolver(
    ISpotifyClientWrapper spotifyClientWrapper,
    ISearchResultFinder searchResultFinder,
    ISpotifyQueryPaginator spotifyQueryPaginator,
    ILogger<SpotifyPodcastResolver> logger)
    : ISpotifyPodcastResolver
{

    public async Task<SpotifyPodcastWrapper?> FindPodcast(
        FindSpotifyPodcastRequest request,
        IndexingContext indexingContext)
    {
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindPodcast)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastId}', Podcast-Name:'{request.Name}'.");
            return null;
        }

        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        var expensiveSpotifyEpisodesQueryFound = false;
        if (!string.IsNullOrWhiteSpace(request.PodcastId))
        {
            var showRequest = new ShowRequest {Market = Market.CountryCode};
            matchingFullShow = await spotifyClientWrapper.GetFullShow(request.PodcastId, showRequest, indexingContext);
        }

        if (matchingFullShow == null)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Show, request.Name);
            var podcasts = await spotifyClientWrapper.GetSearchResponse(searchRequest, indexingContext);
            if (podcasts != null)
            {
                var matchingPodcasts = searchResultFinder.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
                if (request.Episodes.Any())
                {
                    foreach (var candidatePodcast in matchingPodcasts)
                    {
                        var showEpisodesRequest = new ShowEpisodesRequest {Market = Market.CountryCode };
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }

                        var pagedEpisodes =
                            await spotifyClientWrapper.GetShowEpisodes(candidatePodcast.Id, showEpisodesRequest,
                                indexingContext);
                        if (pagedEpisodes != null)
                        {
                            var paginateEpisodesResponse =
                                await spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
                            if (paginateEpisodesResponse.IsExpensiveQuery)
                            {
                                expensiveSpotifyEpisodesQueryFound = true;
                            }

                            if (paginateEpisodesResponse.Results.Any())
                            {
                                var mostRecentEpisode = request.Episodes.OrderByDescending(x => x.Release).First();
                                var matchingEpisode =
                                    searchResultFinder.FindMatchingEpisodeByDate(
                                        mostRecentEpisode.Title.Trim(),
                                        mostRecentEpisode.Release,
                                        new[] {paginateEpisodesResponse.Results});
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
                    FuzzyMatcher.Match(request.Name, matchingPodcasts, x => x.Name);
            }
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow, expensiveSpotifyEpisodesQueryFound);
    }
}