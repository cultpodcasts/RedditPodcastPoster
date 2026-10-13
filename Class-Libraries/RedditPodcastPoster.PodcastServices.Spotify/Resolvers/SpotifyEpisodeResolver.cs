using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text.Sanitisers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

/// <summary>
/// Spotify adapter over the shared <see cref="ICatalogueCandidatePolicy"/>: a known episode id is fetched directly;
/// otherwise the policy sources candidates (recent show feed, then one title-search page) and matches them exactly
/// as for Apple. This class only maps Spotify types and hydrates the match.
/// </summary>
public class SpotifyEpisodeResolver(
    ISpotifyEpisodeTitleSearch episodeTitleSearch,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifyRecentShowEpisodesCache recentShowEpisodesCache,
    ICatalogueCandidatePolicy candidatePolicy,
    IHtmlSanitiser htmlSanitiser,
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
            request, indexingContext, market, episodeTitleSearch, recentShowEpisodesCache, htmlSanitiser);
        var matchingEpisode = await candidatePolicy.FindMatch(ToLookup(request), source, reducer);

        if (matchingEpisode != null)
        {
            var showRequest = new EpisodeRequest { Market = market };
            fullEpisode = await spotifyClientWrapper.GetFullEpisode(matchingEpisode.Id, showRequest, indexingContext);
        }

        return new FindEpisodeResponse(TakeIfFree(fullEpisode, market));
    }

    private static CatalogueLookup ToLookup(FindSpotifyEpisodeRequest request) =>
        new(
            request.PodcastName,
            request.EpisodeTitle,
            request.Released,
            request.Length,
            request.ReleaseAuthority,
            request.EnrichingYouTubeDiscoveredEpisode,
            request.EpisodeDescription,
            request.DefaultSubject,
            request.IgnoredSubjects,
            request.Language,
            request.YouTubePublishingDelay);

    private FullEpisode? TakeIfFree(FullEpisode? episode, string market)
    {
        if (episode == null || episode.IsSpotifyFree())
        {
            return episode;
        }

        SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
        return null;
    }

    /// <summary>Spotify fetch/map adapter: show feed via the pass-cached recent page, search via one title-search page.</summary>
    private sealed class SpotifyCandidateSource(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        string market,
        ISpotifyEpisodeTitleSearch titleSearch,
        ISpotifyRecentShowEpisodesCache recentShowEpisodes,
        IHtmlSanitiser htmlSanitiser) : ICatalogueCandidateSource<SimpleEpisode>
    {
        public string Platform => "Spotify";

        public bool HasShow => !string.IsNullOrWhiteSpace(request.PodcastSpotifyId);

        public Task<IReadOnlyList<SimpleEpisode>> GetRecentShowEpisodes(int pageSize, CancellationToken cancellationToken) =>
            recentShowEpisodes.GetRecentShowEpisodes(request.PodcastSpotifyId, market, pageSize, indexingContext, cancellationToken);

        public Task<IReadOnlyList<SimpleEpisode>> SearchEpisodes(CancellationToken cancellationToken) =>
            titleSearch.FindCandidates(request, indexingContext, market);

        public string GetId(SimpleEpisode item) => item.Id;

        public Episode ToCatalogueEpisode(SimpleEpisode item)
        {
            var mapped = new Episode
            {
                Title = WebUtility.HtmlDecode(item.Name.Trim()),
                Description = htmlSanitiser.Sanitise(item.HtmlDescription ?? string.Empty),
                Length = item.GetDuration(),
                ReleaseUtc = item.GetReleaseDate()
            };
            EpisodeServicePresence.SetSpotifyIdentity(mapped, item.Id);
            return mapped;
        }
    }
}
