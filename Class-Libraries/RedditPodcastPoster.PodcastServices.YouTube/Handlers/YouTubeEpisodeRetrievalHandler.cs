using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Handlers;

public class YouTubeEpisodeRetrievalHandler(
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ILogger<YouTubeEpisodeRetrievalHandler> logger)
    : IYouTubeEpisodeRetrievalHandler
{
    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<EpisodeModel> episodes, IndexingContext indexingContext)
    {
        var handled = false;
        IList<EpisodeModel> newEpisodes = new List<EpisodeModel>();
        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            LogDiscoveryPath(podcast, "skipped-no-channel", indexingContext, 0, null);
            return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
        }

        if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
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
            var getPlaylistEpisodesResult = await youTubeEpisodeProvider.GetPlaylistEpisodes(
                podcast,
                playlistId,
                new YouTubeChannelId(podcast.YouTubeChannelId),
                indexingContext, runExpensivePagination, podcast.YouTubePlaylistOrder,
                KnownPlaylistYouTubeIds(episodes));
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
