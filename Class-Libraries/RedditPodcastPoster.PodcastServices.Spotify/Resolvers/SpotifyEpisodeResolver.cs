using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

public class SpotifyEpisodeResolver(
    ISpotifyEpisodeTitleSearch episodeTitleSearch,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifySearchResultFinder searchResultFinder,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    /// <summary>One page of the show's newest episodes, used when the title search misses.</summary>
    public const int RecentShowEpisodesLimit = 50;

    /// <summary>Only episodes released this recently can be expected on the show's newest page.</summary>
    public static readonly TimeSpan RecentShowEpisodesWindow = TimeSpan.FromDays(14);

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

        var candidates = await episodeTitleSearch.FindCandidates(request, indexingContext, market);
        var matchingEpisode = await MatchCandidates(request, candidates, reducer);
        if (matchingEpisode == null && IsRecentYouTubeEpisodeWithKnownShow(request))
        {
            // The title search queries the incoming (often YouTube) title. When the audio-feed title
            // differs, the real episode is absent from the hits. Apple matches the same pair because it
            // reads the show's episode list, so match against one page of the show's newest episodes too.
            var recent = await spotifyClientWrapper.GetShowEpisodes(
                request.PodcastSpotifyId,
                new ShowEpisodesRequest { Market = market, Limit = RecentShowEpisodesLimit },
                indexingContext);
            var searchedIds = candidates.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var recentCandidates = recent?.Items?
                .Where(x => x != null && !searchedIds.Contains(x.Id))
                .ToList() ?? [];
            matchingEpisode = await MatchCandidates(request, recentCandidates, reducer);
        }

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

    private static bool IsRecentYouTubeEpisodeWithKnownShow(FindSpotifyEpisodeRequest request) =>
        (request.ReleaseAuthority == Service.YouTube || request.EnrichingYouTubeDiscoveredEpisode) &&
        !string.IsNullOrWhiteSpace(request.PodcastSpotifyId) &&
        request.Released is { } released &&
        DateTime.UtcNow - released < RecentShowEpisodesWindow;

    private FullEpisode? TakeIfFree(FullEpisode? episode, string market)
    {
        if (episode == null || episode.IsSpotifyFree())
        {
            return episode;
        }

        SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
        return null;
    }
}
