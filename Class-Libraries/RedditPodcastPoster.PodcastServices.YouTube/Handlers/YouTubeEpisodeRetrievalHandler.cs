using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Handlers;

public class YouTubeEpisodeRetrievalHandler(
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    IYouTubeUploadsHeadCheck uploadsHeadCheck,
    IOptions<YouTubeIndexingHintsOptions> hintsOptions,
    TimeProvider timeProvider,
    ILogger<YouTubeEpisodeRetrievalHandler> logger)
    : IYouTubeEpisodeRetrievalHandler
{
    public const string UploadsHeadDiscoveryPath = "playlist-uploads-head";

    public const string HintsMessagePrefix = "YouTubeIndexingHints";

    private readonly YouTubeIndexingHintsOptions _hintsOptions = hintsOptions?.Value ?? new YouTubeIndexingHintsOptions();

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<EpisodeModel> episodes, IndexingContext indexingContext)
    {
        var handled = false;
        IList<EpisodeModel> newEpisodes = new List<EpisodeModel>();
        if (!podcast.HasYouTubeSource())
        {
            LogDiscoveryPath(podcast, "skipped-no-youtube-source", indexingContext, 0, null);
            return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
        }

        // A playlist is the parent's only YouTube source when set: the channel id is not used (and need not be set).
        if (podcast.UsesYouTubePlaylist())
        {
            var arbitraryOrder = podcast.HasArbitraryYouTubePlaylistOrder();
            var runExpensivePagination = indexingContext.RunExpensiveYouTubePlaylistPagination(podcast);
            // Every non-arbitrary playlist pages (until it leaves the window when newest-first, to the end otherwise);
            // the expensive-query flag only changes the page size. The old "playlist-single-page" label was misleading.
            var discoveryPath = arbitraryOrder
                ? "playlist-arbitrary-full-walk"
                : "playlist-paginated";
            if (!arbitraryOrder && podcast.HasExpensiveYouTubePlaylistQuery() &&
                indexingContext.SkipExpensiveYouTubeQueries)
            {
                logger.LogInformation(
                    "Podcast '{PodcastId}' has known expensive playlist query; expensive-sized pages are not used this pass (smaller batch size, playlist still paginated).",
                    podcast.Id);
            }

            var playlistId = new YouTubePlaylistId(podcast.YouTubePlaylistId,
                YouTubePlaylistIdSource.PodcastEntity, podcast.Id.ToString());
            var knownPlaylistIds = KnownPlaylistYouTubeIds(episodes);
            var startedUtc = timeProvider.GetUtcNow().UtcDateTime;
            var (plan, planReason) =
                YouTubeIndexingHintsPlanner.Plan(podcast, indexingContext, _hintsOptions, startedUtc);
            if (plan == YouTubePlaylistDiscoveryPlan.UploadsHead)
            {
                var uploadsHead = await TryUploadsHead(podcast, playlistId, knownPlaylistIds, indexingContext);
                if (uploadsHead.Episodes != null)
                {
                    LogDiscoveryPath(podcast, UploadsHeadDiscoveryPath, indexingContext, uploadsHead.Episodes.Count,
                        null);
                    return new EpisodeRetrievalHandlerResponse(uploadsHead.Episodes, true);
                }

                if (uploadsHead.FallbackReason == YouTubeUploadsHeadCheck.QuotaExhausted)
                {
                    // Every key is spent; a full walk could not run either. Hints stay as they were.
                    LogDiscoveryPath(podcast, UploadsHeadDiscoveryPath, indexingContext, 0, null);
                    return new EpisodeRetrievalHandlerResponse(newEpisodes, true);
                }

                planReason = uploadsHead.FallbackReason!;
            }

            var getPlaylistEpisodesResult = await youTubeEpisodeProvider.GetPlaylistEpisodes(
                podcast,
                playlistId,
                indexingContext, runExpensivePagination, podcast.YouTubePlaylistOrder,
                knownPlaylistIds);
            if (getPlaylistEpisodesResult.Results != null)
            {
                newEpisodes = getPlaylistEpisodesResult.Results;
            }

            logger.LogPlaylistFetchFailure(podcast, getPlaylistEpisodesResult.Failure, playlistId);

            // Arbitrary-order playlists never yield a head-order probe result (IsExpensiveQuery stays
            // null), and even if a probe value sneaks through the expensive flag must stay untouched —
            // curated playlists have no positional order to learn from.
            if (!arbitraryOrder && getPlaylistEpisodesResult.IsExpensiveQuery.HasValue)
            {
                YouTubeExpensiveQueryFlag.Apply(
                    podcast,
                    getPlaylistEpisodesResult.IsExpensiveQuery,
                    YouTubeExpensiveQueryFlag.MinimumOrderSampleSize,
                    logger);
            }

            ApplyFullWalkHints(podcast, getPlaylistEpisodesResult, startedUtc, planReason);
            LogDiscoveryPath(podcast, discoveryPath, indexingContext, newEpisodes.Count,
                getPlaylistEpisodesResult.KnownSkipped);
        }
        else
        {
            var foundEpisodes = await youTubeEpisodeProvider.GetEpisodes(
                podcast, indexingContext, KnownYouTubeIds(episodes, indexingContext));
            if (foundEpisodes != null)
            {
                newEpisodes = foundEpisodes;
            }

            LogDiscoveryPath(podcast, "channel", indexingContext, newEpisodes.Count, null);
        }

        handled = true;

        return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
    }

    private async Task<(IList<EpisodeModel>? Episodes, string? FallbackReason)> TryUploadsHead(
        Podcast podcast,
        YouTubePlaylistId playlistId,
        IReadOnlyCollection<string> knownPlaylistIds,
        IndexingContext indexingContext)
    {
        var hints = podcast.CurrentYouTubeIndexingHints()!;
        var check = await uploadsHeadCheck.Check(podcast, hints, playlistId, knownPlaylistIds, indexingContext);
        if (!check.Succeeded)
        {
            logger.LogInformation(
                HintsMessagePrefix +
                " podcast-id='{PodcastId}' playlist-id='{PlaylistId}' uploads-head fell back to a full walk: reason='{Reason}' uploads-checked='{UploadsChecked}'.",
                podcast.Id, playlistId.PlaylistId, check.FallbackReason, check.UploadsChecked);
            return (null, check.FallbackReason);
        }

        var built = check.Members.Count == 0
            ? new GetPlaylistEpisodesResponse([])
            : await youTubeEpisodeProvider.BuildPlaylistEpisodes(
                podcast, indexingContext, check.Members, knownPlaylistIds);
        if (built.Results == null)
        {
            const string buildFailed = "build-failed";
            logger.LogInformation(
                HintsMessagePrefix +
                " podcast-id='{PodcastId}' playlist-id='{PlaylistId}' uploads-head fell back to a full walk: reason='{Reason}' uploads-checked='{UploadsChecked}'.",
                podcast.Id, playlistId.PlaylistId, buildFailed, check.UploadsChecked);
            return (null, buildFailed);
        }

        podcast.YouTubeIndexingHints = YouTubeIndexingHintsPlanner.AfterUploadsHead(hints, check);
        logger.LogInformation(
            HintsMessagePrefix +
            " podcast-id='{PodcastId}' playlist-id='{PlaylistId}' uploads-head: count='{Count}' uploads-checked='{UploadsChecked}' members-new='{MembersNew}' checked-through='{CheckedThrough:O}'.",
            podcast.Id, playlistId.PlaylistId, check.TotalResults, check.UploadsChecked, check.Members.Count,
            podcast.YouTubeIndexingHints.UploadsCheckedThrough);
        return (built.Results, null);
    }

    private void ApplyFullWalkHints(
        Podcast podcast,
        GetPlaylistEpisodesResponse walk,
        DateTime startedUtc,
        string planReason)
    {
        var before = podcast.YouTubeIndexingHints;
        var after = YouTubeIndexingHintsPlanner.AfterFullWalk(podcast, walk.WalkOutcome, walk.TotalResults,
            startedUtc, _hintsOptions);
        if (Equals(before, after))
        {
            return;
        }

        podcast.YouTubeIndexingHints = after;
        logger.LogInformation(
            HintsMessagePrefix +
            " podcast-id='{PodcastId}' full walk ({PlanReason}) outcome='{Outcome}' count='{Count}': hints {Change}.",
            podcast.Id, planReason, walk.WalkOutcome, walk.TotalResults,
            after == null ? "cleared" : "recorded");
    }

    /// <summary>
    /// YouTube ids of stored episodes inside the window; the channel path skips them (unchanged behaviour).
    /// </summary>
    private static IReadOnlyCollection<string> KnownYouTubeIds(
        IEnumerable<EpisodeModel> episodes,
        IndexingContext indexingContext)
    {
        var scoped = indexingContext.ReleasedSince.HasValue
            ? episodes.Where(x => x.ReleaseUtc >= indexingContext.ReleasedSince)
            : episodes;
        return scoped.Select(x => EpisodeServicePresence.YouTubeEpisodeId(x) ?? string.Empty).ToArray();
    }

    /// <summary>
    /// YouTube ids the playlist path may skip. Built from every stored episode passed in (the repository already scopes
    /// them to the release window), because the playlist results are windowed on the later of added-at and
    /// video-published-at: an old video recently added to the playlist must still count as known.
    /// A stored episode that merge would still improve stays out of the set, so it is re-fetched and merged as before
    /// the skip existed: <see cref="EpisodePlatformMerger"/> fills a missing YouTube url, image or description
    /// (<c>ApplyFillMissing(ToYouTubePatch())</c>) and <c>YouTubeTimeBackfillMergePolicy</c> backfills a date-only
    /// (midnight) release time. Enrichment does not do either for an episode that already has its YouTube id.
    /// </summary>
    internal static IReadOnlyCollection<string> KnownPlaylistYouTubeIds(IEnumerable<EpisodeModel> episodes)
    {
        return episodes
            .Where(x => !MergeWouldFillMissingYouTubeData(x))
            .Select(x => EpisodeServicePresence.YouTubeEpisodeId(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
    }

    internal static bool MergeWouldFillMissingYouTubeData(EpisodeModel episode)
    {
        return EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube) == null ||
               EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube) == null ||
               string.IsNullOrWhiteSpace(episode.Description) ||
               episode.ReleaseUtc.TimeOfDay == TimeSpan.Zero;
    }

    private void LogDiscoveryPath(Podcast podcast, string discoveryPath, IndexingContext indexingContext,
        int episodesFound, int? knownSkipped)
    {
        // episodes-found counts new (not yet stored) episodes on both paths. known-skipped (playlist path only) counts
        // in-window items dropped because they are already stored; in-window = episodes-found + known-skipped.
        var level = podcast.DependsOnYouTubeForEpisodeDiscovery() ? LogLevel.Warning : LogLevel.Information;
        logger.Log(level,
            "YouTubeDiscoveryPath podcast-id='{PodcastId}' path='{DiscoveryPath}' youtube-authority='{YouTubeAuthority}' skip-youtube='{SkipYouTube}' skip-expensive-youtube='{SkipExpensiveYouTube}' episodes-found='{EpisodesFound}' known-skipped='{KnownSkipped}'",
            podcast.Id, discoveryPath, level == LogLevel.Warning,
            indexingContext.SkipYouTubeUrlResolving, indexingContext.SkipExpensiveYouTubeQueries, episodesFound,
            knownSkipped?.ToString() ?? "n/a");
    }
}
