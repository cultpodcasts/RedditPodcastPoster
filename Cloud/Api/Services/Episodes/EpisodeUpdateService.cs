using System.Diagnostics;
using System.Text.Json;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Reddit.Managers;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

public class EpisodeUpdateService(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    EpisodeChangeApplier episodeChangeApplier,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IHomepagePublisher contentPublisher,
    IPostManager postManager,
    ITweetManager tweetManager,
    IBlueskyPostManager blueskyPostManager,
    IShortnerService shortnerService,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<EpisodeUpdateService> logger) : IEpisodeUpdateService
{
    // Flip to true to emit EpisodeUpdateTiming App Insights warnings (investigation only).
    private const bool EnableDiagnosticTiming = false;

    public async Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        long resolveMs = 0, applyMs = 0, updateImagesMs = 0, saveMs = 0;
        long socialMs = 0, searchDeleteMs = 0, shortnerDeleteMs = 0;
        long indexMs = 0, homepageMs = 0;
        var publishHomepage = false;
        var updateImages = false;
        var removed = false;

        try
        {
            logger.LogInformation("{PostName} Episode Change Request: episode-id: '{EpisodeId}'. {Serialize}",
                nameof(UpdateAsync),
                episodeChangeRequestWrapper.EpisodeId,
                JsonSerializer.Serialize(episodeChangeRequestWrapper.EpisodeChangeRequest));

            var step = Stopwatch.StartNew();
            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    episodeChangeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(UpdateAsync));
            resolveMs = step.ElapsedMilliseconds;

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("Episode with id '{episodeId}' not found.", episodeChangeRequestWrapper.EpisodeId);
                LogTiming(
                    episodeChangeRequestWrapper.EpisodeId,
                    total.ElapsedMilliseconds,
                    resolveMs, applyMs, updateImagesMs, saveMs, socialMs,
                    searchDeleteMs, shortnerDeleteMs, indexMs, homepageMs,
                    publishHomepage, updateImages, removed,
                    EpisodeUpdateStatus.NotFound);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("Podcast with id '{podcastId}' not found for episode-id '{episodeId}'.",
                    podcastEpisodeResolverResponse.Episode.PodcastId, episodeChangeRequestWrapper.EpisodeId);
                LogTiming(
                    episodeChangeRequestWrapper.EpisodeId,
                    total.ElapsedMilliseconds,
                    resolveMs, applyMs, updateImagesMs, saveMs, socialMs,
                    searchDeleteMs, shortnerDeleteMs, indexMs, homepageMs,
                    publishHomepage, updateImages, removed,
                    EpisodeUpdateStatus.NotFound);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            logger.LogInformation(
                "{method} Updating episode-id '{episodeId}' of podcast with id '{podcastId}'. Original-episode: {episode}",
                nameof(UpdateAsync), episodeChangeRequestWrapper.EpisodeId, podcastEpisodeResolverResponse.Podcast.Id,
                JsonSerializer.Serialize(podcastEpisodeResolverResponse.Episode));

            step.Restart();
            var changeState = episodeChangeApplier.Apply(
                podcastEpisodeResolverResponse.Episode,
                episodeChangeRequestWrapper.EpisodeChangeRequest);
            applyMs = step.ElapsedMilliseconds;
            publishHomepage = changeState.PublishHomepage;
            updateImages = changeState.UpdateImages;

            var indexingContext = new IndexingContext();
            if (changeState.UpdateImages)
            {
                step.Restart();
                await imageUpdater.UpdateImages(
                    podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode,
                    new EpisodeImageUpdateRequest(
                        changeState.UpdateSpotifyImage,
                        changeState.UpdateAppleImage,
                        changeState.UpdateYouTubeImage,
                        changeState.UpdateBBCImage),
                    indexingContext);
                updateImagesMs = step.ElapsedMilliseconds;
            }

            step.Restart();
            await episodeRepository.Save(podcastEpisodeResolverResponse.Episode);
            saveMs = step.ElapsedMilliseconds;

            var podcastEpisode = new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                podcastEpisodeResolverResponse.Episode);

            step.Restart();
            if (changeState.UnPost)
            {
                await postManager.RemoveEpisodePost(podcastEpisode);
            }
            else if (changeState.UpdatedSubjects)
            {
                await postManager.UpdateFlare(podcastEpisode);
            }

            var removeTweetResult = RemoveTweetState.Unknown;
            if (changeState.UnTweet)
            {
                try
                {
                    removeTweetResult = await tweetManager.RemoveTweet(podcastEpisode);
                    if (removeTweetResult != RemoveTweetState.Deleted)
                    {
                        logger.LogWarning("Failure to delete tweet. Result= {removeTweetResult}.",
                            removeTweetResult);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        "Error using tweet-manager to remove tweet for episode with id '{episodeId}'.",
                        podcastEpisodeResolverResponse.Episode.Id);
                    removeTweetResult = RemoveTweetState.Other;
                }
            }

            var removeBlueskyPostResult = RemovePostState.Unknown;
            if (changeState.UnBlueskyPost)
            {
                try
                {
                    removeBlueskyPostResult = await blueskyPostManager.RemovePost(podcastEpisode);
                    if (removeBlueskyPostResult != RemovePostState.Deleted)
                    {
                        logger.LogWarning("Failure to delete bluesky-post. Result= {removeBlueskyPostResult}.",
                            removeBlueskyPostResult);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        "Error using bluesky-post-manager to remove post for episode with id '{episodeId}'.",
                        podcastEpisodeResolverResponse.Episode.Id);
                    removeBlueskyPostResult = RemovePostState.Other;
                }
            }

            socialMs = step.ElapsedMilliseconds;

            var outcome = new EpisodeUpdateOutcome();
            if (changeState.UnTweet)
            {
                outcome.TweetDeleted = removeTweetResult == RemoveTweetState.Deleted;
            }

            if (changeState.UnBlueskyPost)
            {
                outcome.BlueskyPostDeleted = removeBlueskyPostResult == RemovePostState.Deleted;
            }

            if (episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.HasValue &&
                episodeChangeRequestWrapper.EpisodeChangeRequest.Removed.Value)
            {
                removed = true;
                step.Restart();
                await searchIndexCleanup.DeleteSearchEntry(
                    podcastEpisodeResolverResponse.Podcast.Name,
                    episodeChangeRequestWrapper.EpisodeId,
                    cancellationToken);
                searchDeleteMs = step.ElapsedMilliseconds;

                step.Restart();
                await shortnerService.Delete(new PodcastEpisode(podcastEpisodeResolverResponse.Podcast,
                    podcastEpisodeResolverResponse.Episode));
                shortnerDeleteMs = step.ElapsedMilliseconds;

                if (changeState.PublishHomepage)
                {
                    step.Restart();
                    await contentPublisher.PublishHomepage();
                    homepageMs = step.ElapsedMilliseconds;
                }
            }
            else
            {
                // Time index and homepage separately even when they run in parallel,
                // so App Insights can show which side dominates wall-clock.
                var indexSw = Stopwatch.StartNew();
                var homepageSw = Stopwatch.StartNew();
                var indexTask = MeasureAsync(
                    () => episodeSearchIndexerService.IndexEpisode(
                        podcastEpisodeResolverResponse.Podcast,
                        podcastEpisodeResolverResponse.Episode,
                        cancellationToken),
                    indexSw);
                var homepageTask = changeState.PublishHomepage
                    ? MeasureAsync(() => contentPublisher.PublishHomepage(), homepageSw)
                    : CompletedWithElapsed(homepageSw);

                await Task.WhenAll(indexTask, homepageTask);
                indexMs = indexSw.ElapsedMilliseconds;
                homepageMs = homepageSw.ElapsedMilliseconds;

                outcome.SearchIndexer = await indexTask;
            }

            LogTiming(
                episodeChangeRequestWrapper.EpisodeId,
                total.ElapsedMilliseconds,
                resolveMs, applyMs, updateImagesMs, saveMs, socialMs,
                searchDeleteMs, shortnerDeleteMs, indexMs, homepageMs,
                publishHomepage, updateImages, removed,
                EpisodeUpdateStatus.Accepted);

            return new EpisodeUpdateResult(EpisodeUpdateStatus.Accepted, outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update episode.", nameof(UpdateAsync));
            LogTiming(
                episodeChangeRequestWrapper.EpisodeId,
                total.ElapsedMilliseconds,
                resolveMs, applyMs, updateImagesMs, saveMs, socialMs,
                searchDeleteMs, shortnerDeleteMs, indexMs, homepageMs,
                publishHomepage, updateImages, removed,
                EpisodeUpdateStatus.Failed);
            return new EpisodeUpdateResult(EpisodeUpdateStatus.Failed);
        }
    }

    /// <summary>
    /// Stable App Insights search key: Message startswith "EpisodeUpdateTiming".
    /// Parallel step clocks (index / homepage) measure each task's own elapsed time.
    /// </summary>
    private void LogTiming(
        Guid episodeId,
        long totalMs,
        long resolveMs,
        long applyMs,
        long updateImagesMs,
        long saveMs,
        long socialMs,
        long searchDeleteMs,
        long shortnerDeleteMs,
        long indexMs,
        long homepageMs,
        bool publishHomepage,
        bool updateImages,
        bool removed,
        EpisodeUpdateStatus status)
    {
        if (!EnableDiagnosticTiming)
        {
            return;
        }

        logger.LogWarning(
            "EpisodeUpdateTiming episode-id='{EpisodeId}' status='{Status}' total-ms='{TotalMs}' resolve-ms='{ResolveMs}' apply-ms='{ApplyMs}' update-images-ms='{UpdateImagesMs}' save-ms='{SaveMs}' social-ms='{SocialMs}' search-delete-ms='{SearchDeleteMs}' shortner-delete-ms='{ShortnerDeleteMs}' index-ms='{IndexMs}' homepage-ms='{HomepageMs}' publish-homepage='{PublishHomepage}' update-images='{UpdateImages}' removed='{Removed}'.",
            episodeId,
            status,
            totalMs,
            resolveMs,
            applyMs,
            updateImagesMs,
            saveMs,
            socialMs,
            searchDeleteMs,
            shortnerDeleteMs,
            indexMs,
            homepageMs,
            publishHomepage,
            updateImages,
            removed);
    }

    private static async Task<T> MeasureAsync<T>(Func<Task<T>> work, Stopwatch stopwatch)
    {
        stopwatch.Restart();
        try
        {
            return await work();
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private static async Task<PublishHomepageResult> MeasureAsync(
        Func<Task<PublishHomepageResult>> work,
        Stopwatch stopwatch)
    {
        stopwatch.Restart();
        try
        {
            return await work();
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private static Task CompletedWithElapsed(Stopwatch stopwatch)
    {
        stopwatch.Reset();
        return Task.CompletedTask;
    }
}
