using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public class SpotifyEpisodeResolver(
    ISpotifyEpisodeTitleSearch episodeTitleSearch,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifySearchResultFinder searchResultFinder,
    ICatalogueCandidatePolicy candidatePolicy,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        Func<SimpleEpisode, bool>? reducer = null)
    {
        var market = request.Market ?? Market.CountryCode;
        if (indexingContext.SkipSpotifyUrlResolving)
        {
            logger.LogInformation(
                "Skipping '{nameofFindEpisode}' as '{nameofSkipSpotifyUrlResolving}' is set. Podcast-Id:'{requestPodcastSpotifyId}', Podcast-Name:'{requestPodcastName}', Episode-Id:'{requestEpisodeSpotifyId}', Episode-Name:'{requestEpisodeTitle}'.",
                nameof(FindEpisode), nameof(indexingContext.SkipSpotifyUrlResolving), request.PodcastSpotifyId,
                request.PodcastName, request.EpisodeSpotifyId, request.EpisodeTitle);
            return new FindEpisodeResponse(null);
        }

        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var episodeRequest = new EpisodeRequest { Market = market };
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(request.EpisodeSpotifyId, episodeRequest, indexingContext);
            if (fullEpisode != null)
            {
                if (!fullEpisode.IsSpotifyFree() &&
                    SpotifyNonPlayableSkipLogger.IsMarketUnavailable(fullEpisode.GetSpotifyRestrictionReason()))
                {
                    SpotifyNonPlayableSkipLogger.LogReturnedDespiteMarket(logger, fullEpisode, market);
                }

                return new FindEpisodeResponse(fullEpisode);
            }
        }

        var source = new SpotifyCandidateSource(
            request, indexingContext, market, episodeTitleSearch, spotifyClientWrapper);
        var matchingEpisode = await candidatePolicy.FindMatch(
            request.Released,
            source,
            candidates => MatchCandidates(request, candidates, reducer));

        if (matchingEpisode != null)
        {
            var showRequest = new EpisodeRequest { Market = market };
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
        }

        return new FindEpisodeResponse(TakeIfFree(fullEpisode, market));
    }

    private async Task<SimpleEpisode?> MatchCandidates(
        FindSpotifyEpisodeRequest request,
        IReadOnlyList<SimpleEpisode> candidates,
        Func<SimpleEpisode, bool>? reducer)
    {
        if (!request.Released.HasValue)
        {
            return await MatchSlice(request, candidates, reducer);
        }

        var inBand = candidates
            .Where(x => EpisodeReleaseTolerance.IsInSubmitMatchBand(x.GetReleaseDate(), request.Released.Value))
            .ToList();
        var outOfBand = candidates
            .Where(x => !EpisodeReleaseTolerance.IsInSubmitMatchBand(x.GetReleaseDate(), request.Released.Value))
            .ToList();
        return await MatchSlice(request, inBand, reducer) ?? await MatchSlice(request, outOfBand, reducer);
    }

    private async Task<SimpleEpisode?> MatchSlice(
        FindSpotifyEpisodeRequest request,
        IReadOnlyList<SimpleEpisode> candidates,
        Func<SimpleEpisode, bool>? reducer)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (request.Length is { } episodeLength && episodeLength > TimeSpan.Zero &&
            (request.ReleaseAuthority == Service.YouTube || request.EnrichingYouTubeDiscoveredEpisode))
        {
            return await searchResultFinder.FindMatchingEpisodeByLength(
                request.EpisodeTitle,
                episodeLength,
                candidates,
                reducer,
                request.ReleaseAuthority,
                request.Released,
                request.EnrichingYouTubeDiscoveredEpisode,
                request.EpisodeDescription,
                request.DefaultSubject,
                request.IgnoredSubjects,
                request.Language);
        }

        return searchResultFinder.FindMatchingEpisodeByDate(
            request.EpisodeTitle,
            request.Released,
            candidates);
    }

    private FullEpisode? TakeIfFree(FullEpisode? episode, string market)
    {
        if (episode == null || episode.IsSpotifyFree())
        {
            return episode;
        }

        SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
        return null;
    }

    /// <summary>Spotify adapter: show feed via GetShowEpisodes, search via one title-search page.</summary>
    private sealed class SpotifyCandidateSource(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        string market,
        ISpotifyEpisodeTitleSearch titleSearch,
        ISpotifyClientWrapper client) : ICatalogueCandidateSource<SimpleEpisode>
    {
        public bool HasShow => !string.IsNullOrWhiteSpace(request.PodcastSpotifyId);

        public async Task<IReadOnlyList<SimpleEpisode>> GetRecentShowEpisodes(int limit)
        {
            var page = await client.GetShowEpisodes(
                request.PodcastSpotifyId,
                new ShowEpisodesRequest { Market = market, Limit = limit },
                indexingContext);
            return page?.Items?.Where(x => x != null).Take(limit).ToList() ?? [];
        }

        public Task<IReadOnlyList<SimpleEpisode>> SearchEpisodes() =>
            titleSearch.FindCandidates(request, indexingContext, market);

        public string GetId(SimpleEpisode item) => item.Id;
    }
}
