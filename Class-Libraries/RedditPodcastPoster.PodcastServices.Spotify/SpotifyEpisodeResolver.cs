using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyEpisodeResolver(
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifySearcher spotifySearcher,
    ISpotifyQueryPaginator spotifyQueryPaginator,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    private static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);

    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext)
    {
        var market = request.Market ?? Market.CountryCode;
        var expensiveQueryFound = false;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(FindEpisode)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.PodcastSpotifyId}', Podcast-Name:'{request.PodcastName}', Episode-Id:'{request.EpisodeSpotifyId}', Episode-Name:'{request.EpisodeTitle}'.");
            return new FindEpisodeResponse(null);
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest
                {Market = market};
            fullEpisode =
                await spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
        }

        if (fullEpisode == null)
        {
            (string, Paging<SimpleEpisode>?)[]? episodes = null;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var showRequest = new ShowRequest {Market = market};
                var fullShow =
                    await spotifyClientWrapper.GetFullShow(request.PodcastSpotifyId, showRequest, indexingContext);
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
                        await spotifyClientWrapper.GetSearchResponse(searchRequest, indexingContext);
                    if (podcastSearchResponse != null)
                    {
                        var podcasts = podcastSearchResponse.Shows.Items;
                        var matchingPodcasts = spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);

                        var showEpisodesRequest = new ShowEpisodesRequest {Market = market};
                        if (indexingContext.ReleasedSince.HasValue)
                        {
                            showEpisodesRequest.Limit = 1;
                        }

                        var episodesFetches = matchingPodcasts
                            .Select(async x => await spotifyClientWrapper
                                .GetShowEpisodes(x.Id, showEpisodesRequest, indexingContext)
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
                        if (indexingContext.SkipExpensiveSpotifyQueries && request.HasExpensiveSpotifyEpisodesQuery)
                        {
                            logger.LogInformation(
                                $"{nameof(FindEpisode)} - Skipping pagination of query results as {nameof(indexingContext.SkipExpensiveSpotifyQueries)} is set.");
                        }
                        else
                        {
                            var paginateEpisodeResponse =
                                await spotifyQueryPaginator.PaginateEpisodes(paging.Item2, indexingContext);
                            var result = paginateEpisodeResponse.Results.GroupBy(x => x.Id).Select(x => x.First());
                            allEpisodes.Add(result.ToList());
                            if (paginateEpisodeResponse.IsExpensiveQuery)
                            {
                                expensiveQueryFound = true;
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning($"Null paged-list of episodes found for spotify-show-id '{paging.Item1}'.");
                    }
                }

                SimpleEpisode? matchingEpisode;
                if (request is {ReleaseAuthority: Service.YouTube, Length: not null})
                {
                    matchingEpisode =
                        spotifySearcher.FindMatchingEpisodeByLength(
                            request.EpisodeTitle,
                            request.Length.Value,
                            allEpisodes,
                            y => request.Released.HasValue &&
                                 Math.Abs((y.GetReleaseDate() - request.Released.Value).Ticks) <
                                 YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks);
                }
                else
                {
                    matchingEpisode =
                        spotifySearcher.FindMatchingEpisodeByDate(request.EpisodeTitle, request.Released, allEpisodes);
                }

                if (matchingEpisode != null)
                {
                    var showRequest = new EpisodeRequest {Market = market};
                    fullEpisode =
                        await spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
                }
            }
        }

        return new FindEpisodeResponse(fullEpisode, expensiveQueryFound);
    }

    public async Task<PaginateEpisodesResponse> GetEpisodes(
        GetEpisodesRequest request,
        IndexingContext indexingContext)
    {
        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetEpisodes)}' as '{nameof(indexingContext.SkipSpotifyUrlResolving)}' is set. Podcast-Id:'{request.SpotifyPodcastId.PodcastId}'.");
            return new PaginateEpisodesResponse(new List<SimpleEpisode>());
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
            return new PaginateEpisodesResponse(pagedEpisodes?.Items ?? new List<SimpleEpisode>());
        }

        return await spotifyQueryPaginator.PaginateEpisodes(pagedEpisodes, indexingContext);
    }
}