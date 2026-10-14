using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Thumbnails;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.Thumbnails;

public class RecentYouTubeThumbnailRechecker(
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IImageUpdater imageUpdater,
    IEpisodeRepository episodeRepository,
    IYouTubeThumbnailRecheckCursorStore cursorStore,
    TimeProvider timeProvider,
    IOptions<YouTubeThumbnailRecheckOptions> options,
    ILogger<RecentYouTubeThumbnailRechecker> logger) : IRecentYouTubeThumbnailRechecker
{
    private static readonly EpisodeImageUpdateRequest YouTubeImageOnly = new(UpdateYouTubeImage: true);

    private readonly YouTubeThumbnailRecheckOptions _options = options.Value;

    public async Task<YouTubeThumbnailRecheckResult> Recheck(
        IReadOnlyList<PodcastEpisode> recentEpisodes,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("YouTubeThumbnailRecheck disabled. {Options}", _options);
            return YouTubeThumbnailRecheckResult.None;
        }

        if (indexingContext.SkipYouTubeUrlResolving || indexingContext.YouTubeQuotaExhausted)
        {
            logger.LogWarning(
                "YouTubeThumbnailRecheck skipped: YouTube resolving unavailable. skip-youtube='{SkipYouTube}' quota-exhausted='{QuotaExhausted}'.",
                indexingContext.SkipYouTubeUrlResolving, indexingContext.YouTubeQuotaExhausted);
            return YouTubeThumbnailRecheckResult.None;
        }

        var runStarted = timeProvider.GetTimestamp();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var releasedSince = now.AddHours(-_options.RecheckWindowHours);
        var eligible = recentEpisodes
            .Select(podcastEpisode => (PodcastEpisode: podcastEpisode,
                Image: EpisodeServicePresence.TryGetImage(podcastEpisode.Episode, ServiceKeys.YouTube)))
            .Where(x => x.Image != null &&
                        YouTubeThumbnailUrls.IsWithdrawableHighResolution(x.Image) &&
                        x.PodcastEpisode.Episode.ReleaseUtc >= releasedSince &&
                        x.PodcastEpisode.Episode.ReleaseUtc <= now)
            .OrderBy(x => x.PodcastEpisode.Episode.ReleaseUtc)
            .ThenBy(x => x.PodcastEpisode.Episode.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            return YouTubeThumbnailRecheckResult.None with { Duration = timeProvider.GetElapsedTime(runStarted) };
        }

        var cursor = await cursorStore.GetAsync(cancellationToken);
        var startIndex = RoundRobinStartIndex(eligible.Select(x => x.PodcastEpisode.Episode).ToList(), cursor);
        var ordered = eligible.Skip(startIndex).Concat(eligible.Take(startIndex)).ToList();

        int probed = 0, stillUsable = 0, replaced = 0, unresolved = 0, deferred = 0, reResolves = 0, failed = 0;
        Episode? lastProbed = null;
        var probeTimeout = TimeSpan.FromSeconds(_options.ProbeTimeoutSeconds);
        var runBudget = TimeSpan.FromSeconds(_options.RunBudgetSeconds);
        for (var index = 0; index < ordered.Count; index++)
        {
            if (probed >= _options.MaxStoredImageProbesPerRun ||
                timeProvider.GetElapsedTime(runStarted) >= runBudget)
            {
                deferred += ordered.Count - index;
                break;
            }

            var (podcastEpisode, storedImage) = ordered[index];
            var episode = podcastEpisode.Episode;
            bool usable;
            probed++;
            try
            {
                using var probeCancellation = new CancellationTokenSource(probeTimeout, timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, probeCancellation.Token);
                usable = await youTubeThumbnailResolver.IsStoredImageUsableAsync(storedImage!, linked.Token);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // A timed-out or failed probe says nothing about withdrawal: count it failed, never re-resolve.
                failed++;
                lastProbed = episode;
                logger.LogError(ex,
                    "YouTubeThumbnailRecheck probe failed. episode-id='{EpisodeId}' podcast-id='{PodcastId}' image='{Image}' timeout-seconds='{TimeoutSeconds}'.",
                    episode.Id, episode.PodcastId, storedImage, _options.ProbeTimeoutSeconds);
                continue;
            }

            if (usable)
            {
                stillUsable++;
                lastProbed = episode;
                continue;
            }

            if (reResolves >= _options.MaxReResolvesPerRun)
            {
                // Withdrawn but the re-resolve budget is spent: stop here so the cursor stays before this
                // episode and the next run re-probes it first.
                probed--;
                deferred += ordered.Count - index;
                break;
            }

            reResolves++;
            lastProbed = episode;
            try
            {
                if (await TryReplace(podcastEpisode, storedImage!, indexingContext))
                {
                    replaced++;
                }
                else
                {
                    unresolved++;
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // One episode's re-resolve or save failure must not stop the rest of the run.
                failed++;
                logger.LogError(ex,
                    "YouTubeThumbnailRecheck failed for episode. episode-id='{EpisodeId}' podcast-id='{PodcastId}' image='{Image}'.",
                    episode.Id, episode.PodcastId, storedImage);
            }
        }

        YouTubeThumbnailRecheckCursor? savedCursor = null;
        if (lastProbed != null)
        {
            savedCursor = new YouTubeThumbnailRecheckCursor
            {
                LastProbedReleaseUtc = lastProbed.ReleaseUtc,
                LastProbedEpisodeId = lastProbed.Id,
                UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            try
            {
                await cursorStore.SaveAsync(savedCursor, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Losing the cursor only means the next run restarts from the oldest; never fail the run.
                logger.LogError(ex, "YouTubeThumbnailRecheck failed to save the round-robin cursor.");
            }
        }

        return new YouTubeThumbnailRecheckResult(
            eligible.Count, probed, stillUsable, replaced, unresolved, deferred, failed)
        {
            Duration = timeProvider.GetElapsedTime(runStarted),
            CursorEpisodeId = savedCursor?.LastProbedEpisodeId ?? cursor?.LastProbedEpisodeId,
            CursorReleaseUtc = savedCursor?.LastProbedReleaseUtc ?? cursor?.LastProbedReleaseUtc
        };
    }

    /// <summary>
    /// Index of the first episode strictly after the cursor (release time, then id) in the ordered eligible
    /// list; 0 when there is no cursor or the cursor is at or past the end (wrap). A cursor whose episode has
    /// left the window still positions by its release time.
    /// </summary>
    internal static int RoundRobinStartIndex(IReadOnlyList<Episode> ordered, YouTubeThumbnailRecheckCursor? cursor)
    {
        if (cursor?.LastProbedReleaseUtc is not { } cursorRelease)
        {
            return 0;
        }

        var cursorId = cursor.LastProbedEpisodeId ?? Guid.Empty;
        for (var i = 0; i < ordered.Count; i++)
        {
            var release = ordered[i].ReleaseUtc;
            if (release > cursorRelease || (release == cursorRelease && ordered[i].Id.CompareTo(cursorId) > 0))
            {
                return i;
            }
        }

        return 0;
    }
    /// <summary>
    /// Re-resolves on the run's snapshot (so posting later in the run uses the new image), then applies
    /// only the YouTube image to a fresh read of the stored episode before saving, so edits made since
    /// the snapshot loaded are not overwritten.
    /// </summary>
    private async Task<bool> TryReplace(PodcastEpisode podcastEpisode, Uri storedImage, IndexingContext indexingContext)
    {
        var snapshot = podcastEpisode.Episode;
        if (!await imageUpdater.UpdateImages(podcastEpisode.Podcast, snapshot, YouTubeImageOnly, indexingContext))
        {
            logger.LogWarning(
                "YouTubeThumbnailRecheck kept withdrawn image: no usable replacement. episode-id='{EpisodeId}' podcast-id='{PodcastId}' image='{Image}'.",
                snapshot.Id, snapshot.PodcastId, storedImage);
            return false;
        }

        var newImage = EpisodeServicePresence.TryGetImage(snapshot, ServiceKeys.YouTube)!;
        var stored = await episodeRepository.GetEpisode(snapshot.PodcastId, snapshot.Id);
        if (stored == null)
        {
            logger.LogWarning(
                "YouTubeThumbnailRecheck skipped save: episode no longer stored. episode-id='{EpisodeId}' podcast-id='{PodcastId}'.",
                snapshot.Id, snapshot.PodcastId);
            return false;
        }

        EpisodeServicePresence.Upsert(
            stored,
            ServiceKeys.YouTube,
            EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.YouTube),
            newImage);
        await episodeRepository.Save(stored);
        logger.LogWarning(
            "YouTubeThumbnailRecheck replaced withdrawn image. episode-id='{EpisodeId}' podcast-id='{PodcastId}' old-image='{OldImage}' new-image='{NewImage}'.",
            snapshot.Id, snapshot.PodcastId, storedImage, newImage);
        return true;
    }
}
