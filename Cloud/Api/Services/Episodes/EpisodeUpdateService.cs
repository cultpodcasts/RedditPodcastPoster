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
            if (request.AdditionalYouTube is not null)
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
    /// Applies the change, runs social deletes, then saves. Null when <paramref name="etag"/> was set and the
    /// replace lost the race, so the caller reads again.
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

        var podcastEpisode = new PodcastEpisode(podcast, episode);

        var removeBlueskyPostResult = RemovePostState.Unknown;
        if (changeState.UnBlueskyPost)
        {
            try
            {
                // Delete first while BlueskyPost AT URI is still on the episode.
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

            // Only flip Cosmostate when the remote post is gone (or already absent).
            if (removeBlueskyPostResult is RemovePostState.Deleted or RemovePostState.NotFound)
            {
                podcastEpisode.Episode.ClearBlueskyPostState();
            }
        }

        var removeTweetResult = RemoveTweetState.Unknown;
        if (changeState.UnTweet)
        {
            try
            {
                // Delete first, before Tweeted and the stored tweet id are cleared and saved (#1204).
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

            // Only clear Tweeted when the tweet is gone (or already absent); otherwise the hourly job would
            // re-post an item whose tweet is still live.
            if (removeTweetResult is RemoveTweetState.Deleted or RemoveTweetState.NotFound)
            {
                podcastEpisode.Episode.ClearTweetState();
            }
        }

        if (etag is null)
        {
            await episodeRepository.Save(episode);
        }
        else if (!await episodeRepository.TryReplace(episode, etag))
        {
            return null;
        }

        // Reddit.NET live un-post / flair sync is retired. Cosmos `posted` is already
        // applied via EpisodeChangeApplier; title/comment constructors remain for Devvit.

        var outcome = new EpisodeUpdateOutcome();
        if (changeState.UnTweet)
        {
            outcome.TweetDeleted = removeTweetResult is RemoveTweetState.Deleted or RemoveTweetState.NotFound;
        }

        if (changeState.UnBlueskyPost)
        {
            outcome.BlueskyPostDeleted = removeBlueskyPostResult is RemovePostState.Deleted
                or RemovePostState.NotFound;
        }

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

    /// <summary>A detached copy of the stored episode, for removal matching only.</summary>
    private static Episode SnapshotForRemoval(Episode episode) =>
        JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(episode))
        ?? throw new InvalidOperationException($"Could not copy episode '{episode.Id}'.");
}
