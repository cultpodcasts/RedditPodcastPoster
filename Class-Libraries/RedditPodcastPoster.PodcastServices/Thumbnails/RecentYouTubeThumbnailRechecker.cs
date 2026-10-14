using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.Thumbnails;

public class RecentYouTubeThumbnailRechecker(
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IImageUpdater imageUpdater,
    IEpisodeRepository episodeRepository,
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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var releasedSince = now.AddHours(-_options.RecheckWindowHours);
        var eligible = recentEpisodes
            .Select(podcastEpisode => (PodcastEpisode: podcastEpisode,
                Image: EpisodeServicePresence.TryGetImage(podcastEpisode.Episode, ServiceKeys.YouTube)))
            .Where(x => x.Image != null &&
                        YouTubeThumbnailUrls.IsWithdrawableHighResolution(x.Image) &&
                        x.PodcastEpisode.Episode.ReleaseUtc >= releasedSince &&
                        x.PodcastEpisode.Episode.ReleaseUtc <= now)
            .OrderByDescending(x => x.PodcastEpisode.Episode.ReleaseUtc)
            .ToList();

        int probed = 0, stillUsable = 0, replaced = 0, unresolved = 0, deferred = 0, reResolves = 0;
        foreach (var (podcastEpisode, storedImage) in eligible)
        {
            if (probed >= _options.MaxStoredImageProbesPerRun)
            {
                deferred++;
                continue;
            }

            probed++;
            if (await youTubeThumbnailResolver.IsStoredImageUsableAsync(storedImage!, cancellationToken))
            {
                stillUsable++;
                continue;
            }

            if (reResolves >= _options.MaxReResolvesPerRun)
            {
                deferred++;
                continue;
            }

            reResolves++;
            var episode = podcastEpisode.Episode;
            if (await imageUpdater.UpdateImages(podcastEpisode.Podcast, episode, YouTubeImageOnly, indexingContext))
            {
                await episodeRepository.Save(episode);
                replaced++;
                logger.LogWarning(
                    "YouTubeThumbnailRecheck replaced withdrawn image. episode-id='{EpisodeId}' podcast-id='{PodcastId}' old-image='{OldImage}' new-image='{NewImage}'.",
                    episode.Id, episode.PodcastId, storedImage,
                    EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube));
            }
            else
            {
                unresolved++;
                logger.LogWarning(
                    "YouTubeThumbnailRecheck kept withdrawn image: no usable replacement. episode-id='{EpisodeId}' podcast-id='{PodcastId}' image='{Image}'.",
                    episode.Id, episode.PodcastId, storedImage);
            }
        }

        var result = new YouTubeThumbnailRecheckResult(
            eligible.Count, probed, stillUsable, replaced, unresolved, deferred);
        logger.LogWarning(
            "YouTubeThumbnailRecheck complete. eligible='{Eligible}' probed='{Probed}' still-usable='{StillUsable}' replaced='{Replaced}' unresolved='{Unresolved}' deferred='{Deferred}' window-hours='{WindowHours}'.",
            result.Eligible, result.Probed, result.StillUsable, result.Replaced, result.Unresolved,
            result.DeferredOverBudget, _options.RecheckWindowHours);
        return result;
    }
}
