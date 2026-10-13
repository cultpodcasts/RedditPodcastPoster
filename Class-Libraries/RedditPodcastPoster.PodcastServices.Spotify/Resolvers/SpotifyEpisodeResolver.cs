using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
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
/// Spotify adapter: a known episode id is fetched directly (step 0); otherwise the shared
/// <see cref="ICatalogueLookupExecutor"/> plans and runs the strategy chain over <see cref="SpotifyCandidateSource"/>,
/// exactly as for Apple. This class only maps Spotify types and hydrates the match with <c>GetFullEpisode</c>.
/// </summary>
public class SpotifyEpisodeResolver(
    ISpotifyEpisodeTitleSearch episodeTitleSearch,
    ISpotifyClientWrapper spotifyClientWrapper,
    ISpotifyShowEpisodePages showEpisodePages,
    ICatalogueLookupExecutor lookupExecutor,
    ICatalogueRequestGuard guard,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifyEpisodeResolver> logger)
    : ISpotifyEpisodeResolver
{
    public const string Platform = CataloguePlatforms.Spotify;


    public async Task<FindEpisodeResponse> FindEpisode(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        Func<SimpleEpisode, bool>? reducer = null,
        CancellationToken cancellationToken = default)
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

        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            var direct = await GetFullEpisode(request.EpisodeSpotifyId, market, indexingContext, cancellationToken);
            if (direct != null)
            {
                if (!direct.IsSpotifyFree() &&
                    SpotifyNonPlayableSkipLogger.IsMarketUnavailable(direct.GetSpotifyRestrictionReason()))
                {
                    SpotifyNonPlayableSkipLogger.LogReturnedDespiteMarket(logger, direct, market);
                }

                return new FindEpisodeResponse(direct);
            }

            // A stale or invalid stored id must not block matching (same rule as Apple).
        }

        var source = new SpotifyCandidateSource(
            request, indexingContext, market, episodeTitleSearch, showEpisodePages, htmlSanitiser);
        var result = await lookupExecutor.FindMatch(ToLookup(request, indexingContext), source, reducer, cancellationToken);
        if (result.Match == null)
        {
            return new FindEpisodeResponse(null);
        }

        var full = await GetFullEpisode(result.Match.Id, market, indexingContext, cancellationToken);
        return new FindEpisodeResponse(TakeIfFree(full, market));
    }

    public static CatalogueLookup ToLookup(FindSpotifyEpisodeRequest request, IndexingContext indexingContext) =>
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
            request.YouTubePublishingDelay)
        {
            Flow = indexingContext.LookupFlow,
            WindowStart = indexingContext.ReleasedSince,
            ShowEpisodeCount = request.ShowEpisodeCount
        };

    private Task<FullEpisode?> GetFullEpisode(
        string episodeId, string market, IndexingContext indexingContext, CancellationToken cancellationToken) =>
        guard.Run(
            Platform,
            $"GetFullEpisode episode='{episodeId}'",
            ct => spotifyClientWrapper.GetFullEpisode(episodeId, new EpisodeRequest { Market = market }, indexingContext, ct),
            cancellationToken);

    private FullEpisode? TakeIfFree(FullEpisode? episode, string market)
    {
        if (episode == null || episode.IsSpotifyFree())
        {
            return episode;
        }

        SpotifyNonPlayableSkipLogger.Log(logger, episode, market);
        return null;
    }

    /// <summary>Spotify fetch/map adapter: pass-cached newest-first show pages and one title-search page.</summary>
    private sealed class SpotifyCandidateSource(
        FindSpotifyEpisodeRequest request,
        IndexingContext indexingContext,
        string market,
        ISpotifyEpisodeTitleSearch titleSearch,
        ISpotifyShowEpisodePages showEpisodePages,
        IHtmlSanitiser htmlSanitiser) : ICatalogueCandidateSource<SimpleEpisode>
    {
        public string Platform => SpotifyEpisodeResolver.Platform;

        public string? ShowId =>
            string.IsNullOrWhiteSpace(request.PodcastSpotifyId) ? null : request.PodcastSpotifyId;


        /// <summary>Free: page 0 is needed by most plans anyway and carries <c>Paging.Total</c>.</summary>
        public async Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken) =>
            (await GetNewestPage(0, cancellationToken))?.TotalEpisodes;

        private Task<CatalogueShowPage<SimpleEpisode>?>? _newestPage;

        /// <remarks>
        /// Page 0 is memoised for this lookup, including a failed (null) read, so the size probe and the first strategy
        /// never repeat a request that has just timed out.
        /// </remarks>
        public Task<CatalogueShowPage<SimpleEpisode>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken)
        {
            if (ShowId is not { } showId)
            {
                return Task.FromResult<CatalogueShowPage<SimpleEpisode>?>(null);
            }

            return pageIndex == 0
                ? _newestPage ??= showEpisodePages.GetNewestPage(showId, market, 0, indexingContext, cancellationToken)
                : showEpisodePages.GetNewestPage(showId, market, pageIndex, indexingContext, cancellationToken);
        }

        public Task<IReadOnlyList<SimpleEpisode>> SearchByTitle(CancellationToken cancellationToken) =>
            titleSearch.FindCandidates(request, indexingContext, market, cancellationToken);

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
