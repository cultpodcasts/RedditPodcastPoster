using System.Text.Json;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Api.Services;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Models;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

public class EpisodeUpdateService(
    IEpisodeRepository episodeRepository,
    IPodcastEpisodeResolver podcastEpisodeResolver,
    EpisodeChangeApplier episodeChangeApplier,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IHomepagePublisher contentPublisher,
    ITweetManager tweetManager,
    IBlueskyPostManager blueskyPostManager,
    IShortnerService shortnerService,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    IUrlMembershipLookup urlMembershipLookup,
    ILogger<EpisodeUpdateService> logger) : IEpisodeUpdateService
{
    public async Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{PostName} Episode Change Request: episode-id: '{EpisodeId}'. {Serialize}",
                nameof(UpdateAsync),
                episodeChangeRequestWrapper.EpisodeId,
                JsonSerializer.Serialize(episodeChangeRequestWrapper.EpisodeChangeRequest));

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    episodeChangeRequestWrapper.ToPodcastEpisodeResolverRequest(), nameof(UpdateAsync));

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("Episode with id '{episodeId}' not found.", episodeChangeRequestWrapper.EpisodeId);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("Podcast with id '{podcastId}' not found for episode-id '{episodeId}'.",
                    podcastEpisodeResolverResponse.Episode.PodcastId, episodeChangeRequestWrapper.EpisodeId);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            logger.LogInformation(
                "{method} Updating episode-id '{episodeId}' of podcast with id '{podcastId}'. Original-episode: {episode}",
                nameof(UpdateAsync), episodeChangeRequestWrapper.EpisodeId, podcastEpisodeResolverResponse.Podcast.Id,
                JsonSerializer.Serialize(podcastEpisodeResolverResponse.Episode));

            var splitOffReviewError = EpisodeChangeApplier.SplitOffReviewError(
                podcastEpisodeResolverResponse.Episode,
                episodeChangeRequestWrapper.EpisodeChangeRequest);
            if (splitOffReviewError != null)
            {
                logger.LogWarning("Episode change for episode-id '{EpisodeId}' rejected: {Reason}",
                    episodeChangeRequestWrapper.EpisodeId, splitOffReviewError);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.BadRequest, Message: splitOffReviewError);
            }

            var request = episodeChangeRequestWrapper.EpisodeChangeRequest;
            var podcast = podcastEpisodeResolverResponse.Podcast;
            var episode = podcastEpisodeResolverResponse.Episode;
            // Promote rewrites additionalYouTube. A keep-list and a promote-only both re-read and replace, so a
            // stale first read cannot drop an alternate added after it. An unknown promote id is a 400.
            if (request.AdditionalYouTube is not null || !string.IsNullOrWhiteSpace(request.PromoteYouTubeId))
            {
                return await UpdateAdditionalYouTube(podcast, episode, request, cancellationToken);
            }

            return await ApplySaveAndPublish(podcast, episode, request, etag: null, cancellationToken)
                   ?? new EpisodeUpdateResult(EpisodeUpdateStatus.Failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update episode.", nameof(UpdateAsync));
            return new EpisodeUpdateResult(EpisodeUpdateStatus.Failed);
        }
    }

    private async Task<EpisodeUpdateResult> UpdateAdditionalYouTube(
        Podcast podcast,
        Episode firstRead,
        EpisodeChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!YouTubeAlternateChangeApplier.TryValidate(
                firstRead,
                request.AdditionalYouTube,
                request.PromoteYouTubeId,
                EpisodeChangeApplier.ResultingYouTubePrimary(firstRead, request),
                "episode",
                rejectId: null,
                out var error))
        {
            return new EpisodeUpdateResult(EpisodeUpdateStatus.BadRequest, Message: error);
        }

        var heldOnFirstRead = await YouTubeConflict(firstRead, request, cancellationToken);
        if (heldOnFirstRead is not null)
        {
            return new EpisodeUpdateResult(EpisodeUpdateStatus.Conflict, Message: heldOnFirstRead);
        }

        for (var attempt = 1; attempt <= FilmDocumentUpdate.MaxAttempts; attempt++)
        {
            var latest = await episodeRepository.GetEpisodeWithETag(firstRead.PodcastId, firstRead.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: Episode '{episodeId}' was deleted during the edit; not saved.",
                    nameof(UpdateAdditionalYouTube), firstRead.Id);
                return new EpisodeUpdateResult(EpisodeUpdateStatus.NotFound);
            }

            // A promote-only edit does not send a keep-list, so a concurrent alternate is not a 409.
            if (request.AdditionalYouTube is not null)
            {
                var unexpected = YouTubeAlternateChangeApplier.IdsTheClientDidNotList(
                    latest.Item.AdditionalYouTube, request.ExpectedAdditionalYouTubeIds);
                if (unexpected.Length > 0)
                {
                    logger.LogWarning(
                        "{method}: Episode '{episodeId}' has {count} additional YouTube id(s) this edit did not list; not saved.",
                        nameof(UpdateAdditionalYouTube), firstRead.Id, unexpected.Length);
                    return new EpisodeUpdateResult(
                        EpisodeUpdateStatus.Conflict,
                        Message: YouTubeAlternateChangeApplier.StaleMessage("episode"),
                        UnexpectedAdditionalYouTubeIds: unexpected);
                }
            }

            var splitOff = EpisodeChangeApplier.SplitOffReviewError(latest.Item, request);
            if (splitOff != null)
            {
                return new EpisodeUpdateResult(EpisodeUpdateStatus.BadRequest, Message: splitOff);
            }

            if (!YouTubeAlternateChangeApplier.TryValidate(
                    latest.Item,
                    request.AdditionalYouTube,
                    request.PromoteYouTubeId,
                    EpisodeChangeApplier.ResultingYouTubePrimary(latest.Item, request),
                    "episode",
                    rejectId: null,
                    out var latestError))
            {
                return new EpisodeUpdateResult(EpisodeUpdateStatus.BadRequest, Message: latestError);
            }

            var held = await YouTubeConflict(latest.Item, request, cancellationToken);
            if (held is not null)
            {
                return new EpisodeUpdateResult(EpisodeUpdateStatus.Conflict, Message: held);
            }

            var saved = await ApplySaveAndPublish(podcast, latest.Item, request, latest.ETag, cancellationToken);
            if (saved is not null)
            {
                return saved;
            }
        }

        throw new InvalidOperationException(
            $"Episode '{firstRead.Id}' kept changing during the edit; not saved.");
    }

    private Task<string?> YouTubeConflict(
        Episode episode,
        EpisodeChangeRequest request,
        CancellationToken cancellationToken) =>
        YouTubeAlternateOwnership.ConflictMessageIfHeld(
            urlMembershipLookup,
            YouTubeAlternateChangeApplier.NewIds(
                episode,
                request.AdditionalYouTube,
                EpisodeChangeApplier.ResultingYouTubePrimary(episode, request)),
            [episode.Id],
            cancellationToken);

    /// <summary>
    /// Applies the change, then saves. A replace that loses the race returns null so the caller reads again,
    /// and has not deleted a social post. Social deletes run after a successful replace (or, when there is no
    /// etag, before the single save, which cannot 409 on a second attempt). Cleared flags are patched onto a
    /// later read so a rename during the delete is not overwritten.
    /// </summary>
    private async Task<EpisodeUpdateResult?> ApplySaveAndPublish(
        Podcast podcast,
        Episode episode,
        EpisodeChangeRequest request,
        string? etag,
        CancellationToken cancellationToken)
    {
        var hadAdditionalYouTube = episode.AdditionalYouTube is { Count: > 0 };
        // Taken before the change is applied: a legacy un-tweet (no stored tweet id) searches X for the post by the
        // release and length it was made with, not the ones this request writes (#1204). Never saved.
        var tweetRemovalTarget = request.Tweeted == false && episode.Tweeted
            ? new PodcastEpisode(podcast, SnapshotForRemoval(episode))
            : null;

        var changeState = episodeChangeApplier.Apply(episode, request);

        var indexingContext = new IndexingContext();
        if (changeState.UpdateImages)
        {
            await imageUpdater.UpdateImages(
                podcast,
                episode,
                new EpisodeImageUpdateRequest(
                    changeState.UpdateSpotifyImage,
                    changeState.UpdateAppleImage,
                    changeState.UpdateYouTubeImage,
                    changeState.UpdateBBCImage),
                indexingContext);
        }

        var outcome = new EpisodeUpdateOutcome();
        if (etag is null)
        {
            // One save, no second 409. Delete first so the stored flags drop only when the post is gone (#1204).
            outcome = await RetractSocial(podcast, episode, changeState, tweetRemovalTarget);
            await episodeRepository.Save(episode);
        }
        else if (!await episodeRepository.TryReplace(episode, etag))
        {
            return null;
        }
        else
        {
            outcome = await RetractSocial(podcast, episode, changeState, tweetRemovalTarget);
            if (outcome.TweetDeleted == true || outcome.BlueskyPostDeleted == true)
            {
                var patched = await PersistClearedSocialFlags(episode, outcome);
                if (patched is not null)
                {
                    episode = patched;
                }
            }
        }

        // Reddit.NET live un-post / flair sync is retired. Cosmos `posted` is already
        // applied via EpisodeChangeApplier; title/comment constructors remain for Devvit.

        try
        {
            // #1484: rejecting a split-off hides it, as removing does.
            if (request.Removed == true || request.ApprovalDecision == ApprovalDecision.Reject)
            {
                await searchIndexCleanup.DeleteSearchEntry(podcast.Name, episode.Id, cancellationToken);
                await shortnerService.Delete(new PodcastEpisode(podcast, episode));

                if (changeState.PublishHomepage)
                {
                    await contentPublisher.PublishHomepage();
                }
            }
            else
            {
                var youTubeAlternatesCleared =
                    hadAdditionalYouTube && episode.AdditionalYouTube is not { Count: > 0 };
                var indexTask = youTubeAlternatesCleared
                    ? episodeSearchIndexerService.IndexEpisode(podcast, episode, cancellationToken, true)
                    : episodeSearchIndexerService.IndexEpisode(podcast, episode, cancellationToken);
                var homepageTask = changeState.PublishHomepage
                    ? contentPublisher.PublishHomepage()
                    : Task.CompletedTask;

                await Task.WhenAll(indexTask, homepageTask);

                outcome.SearchIndexer = await indexTask;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Cosmos save succeeded for episode-id '{episodeId}' but search-index or homepage publish failed. Returning Accepted so a curator retry is not required.",
                nameof(UpdateAsync), episode.Id);
        }

        return new EpisodeUpdateResult(EpisodeUpdateStatus.Accepted, outcome);
    }

    /// <summary>
    /// Deletes the Bluesky post and the tweet, and clears those flags on <paramref name="episode"/> only when the
    /// remote post is gone. Does not write Cosmos.
    /// </summary>
    private async Task<EpisodeUpdateOutcome> RetractSocial(
        Podcast podcast,
        Episode episode,
        EpisodeChangeState changeState,
        PodcastEpisode? tweetRemovalTarget)
    {
        var podcastEpisode = new PodcastEpisode(podcast, episode);
        var outcome = new EpisodeUpdateOutcome();

        if (changeState.UnBlueskyPost)
        {
            var removeBlueskyPostResult = RemovePostState.Unknown;
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
                    episode.Id);
                removeBlueskyPostResult = RemovePostState.Other;
            }

            outcome.BlueskyPostDeleted = removeBlueskyPostResult is RemovePostState.Deleted or RemovePostState.NotFound;
            if (outcome.BlueskyPostDeleted == true)
            {
                episode.ClearBlueskyPostState();
            }
        }

        if (changeState.UnTweet)
        {
            var removeTweetResult = RemoveTweetState.Unknown;
            try
            {
                removeTweetResult = await tweetManager.RemoveTweet(tweetRemovalTarget ?? podcastEpisode);
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
                    episode.Id);
                removeTweetResult = RemoveTweetState.Other;
            }

            outcome.TweetDeleted = removeTweetResult is RemoveTweetState.Deleted or RemoveTweetState.NotFound;
            if (outcome.TweetDeleted == true)
            {
                episode.ClearTweetState();
            }
        }

        return outcome;
    }

    /// <summary>
    /// Writes cleared tweet and Bluesky flags onto a fresh read, so a field changed during the delete stays.
    /// </summary>
    private async Task<Episode?> PersistClearedSocialFlags(Episode saved, EpisodeUpdateOutcome outcome)
    {
        for (var attempt = 1; attempt <= FilmDocumentUpdate.MaxAttempts; attempt++)
        {
            var latest = await episodeRepository.GetEpisodeWithETag(saved.PodcastId, saved.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: Episode '{episodeId}' was deleted while clearing social flags.",
                    nameof(PersistClearedSocialFlags), saved.Id);
                return null;
            }

            if (outcome.TweetDeleted == true)
            {
                latest.Item.ClearTweetState();
            }

            if (outcome.BlueskyPostDeleted == true)
            {
                latest.Item.ClearBlueskyPostState();
            }

            if (await episodeRepository.TryReplace(latest.Item, latest.ETag))
            {
                return latest.Item;
            }
        }

        throw new InvalidOperationException(
            $"Episode '{saved.Id}' kept changing while clearing social flags; not saved.");
    }

    /// <summary>A detached copy of the stored episode, for removal matching only.</summary>
    private static Episode SnapshotForRemoval(Episode episode) =>
        JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(episode))
        ?? throw new InvalidOperationException($"Could not copy episode '{episode.Id}'.");
}
