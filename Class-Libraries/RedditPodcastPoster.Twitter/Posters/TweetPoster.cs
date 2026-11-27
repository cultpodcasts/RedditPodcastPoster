using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Posters;

public class TweetPoster(
    IEpisodeRepository episodeRepository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    ICataloguePlayableSocialPosts cataloguePlayableSocialPosts,
    ITweetSaveBackoff saveBackoff,
    ILogger<TweetPoster> logger)
    : ITweetPoster
{
    /// <summary>Attempts to save a document after X accepted its post (#1205).</summary>
    public const int MaxSaveAttempts = 3;

    public async Task<PostTweetResponse> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl, bool hasShareImage = false)
    {
        string tweet;
        try
        {
            tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortUrl, hasShareImage);
        }
        catch (UntweetableException ex)
        {
            logger.LogWarning(
                "Marking {ContentKind} id '{EpisodeId}' (parent '{PodcastId}') as not tweetable reason='{Reason}': {Message}",
                podcastEpisode.ContentKind ?? "episode",
                podcastEpisode.Episode.Id,
                podcastEpisode.Podcast.Id,
                ex.Reason,
                ex.Message);
            await PersistNotTweetable(podcastEpisode, ex.Reason);
            return new PostTweetResponse(TweetSendStatus.Untweetable);
        }

        PostTweetResponse tweetStatus;
        try
        {
            tweetStatus = await twitterClient.Send(tweet);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to send tweet for podcast-id '{PodcastId}' episode-id '{EpisodeId}', tweet: '{Tweet}'.", podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, tweet);
            throw;
        }

        if (tweetStatus.TweetSendStatus is TweetSendStatus.Sent or TweetSendStatus.DuplicateForbidden)
        {
            // A new post stores the id only, the same way a Bluesky post stores blueskyPost and not the legacy flag.
            // A duplicate-content refusal has no new id, so the legacy flag records that it must not be posted again.
            if (tweetStatus.TweetSendStatus == TweetSendStatus.Sent && !string.IsNullOrWhiteSpace(tweetStatus.TweetId))
            {
                var previousTweetId = podcastEpisode.Episode.TweetPost;
                if (!string.IsNullOrWhiteSpace(previousTweetId) && previousTweetId != tweetStatus.TweetId)
                {
                    logger.LogWarning(
                        "Episode-id '{EpisodeId}' of podcast-id '{PodcastId}' already had tweet-id '{PreviousTweetId}'; replacing it with new tweet-id '{TweetId}'. The older post may still be live.",
                        podcastEpisode.Episode.Id, podcastEpisode.Podcast.Id, previousTweetId, tweetStatus.TweetId);
                }

                podcastEpisode.Episode.TweetPost = tweetStatus.TweetId;
            }
            else if (string.IsNullOrWhiteSpace(podcastEpisode.Episode.TweetPost))
            {
                podcastEpisode.Episode.OldTweeted = true;
            }

            var saveFailure = await SaveWithRetry(podcastEpisode);
            if (saveFailure == null)
            {
                return tweetStatus;
            }

            if (tweetStatus.TweetSendStatus == TweetSendStatus.Sent)
            {
                // The post is live but unrecorded. Report it instead of throwing so the run counts the post and
                // stops: continuing would post the next candidate while persistence is failing, and the same
                // candidate would be posted again next run (#1205).
                logger.LogError(saveFailure,
                    "Tweet-id '{TweetId}' for podcast-id '{PodcastId}' episode-id '{EpisodeId}' was posted to X but the episode could not be saved as tweeted after {Attempts} attempts; the post is live and the episode may be tweeted again.",
                    tweetStatus.TweetId, podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, MaxSaveAttempts);
                return tweetStatus with {TweetSendStatus = TweetSendStatus.SentNotSaved};
            }

            ExceptionDispatchInfo.Capture(saveFailure).Throw();
        }

        return tweetStatus;
    }

    /// <summary>
    /// Saves the tweeted state, retrying with backoff. Returns the last failure when every attempt failed.
    /// </summary>
    private async Task<Exception?> SaveWithRetry(PodcastEpisode podcastEpisode)
    {
        Exception? lastFailure = null;
        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            try
            {
                if (podcastEpisode.IsCataloguePlayable)
                {
                    await cataloguePlayableSocialPosts.MarkTweeted(podcastEpisode, podcastEpisode.Episode.TweetPost);
                }
                else
                {
                    await episodeRepository.Save(podcastEpisode.Episode);
                }

                return null;
            }
            catch (Exception ex)
            {
                lastFailure = ex;
                var contentKind = podcastEpisode.ContentKind ?? "episode";
                logger.LogError(ex,
                    "Failure to save {ContentKind} with podcast-id '{PodcastId}' and episode-id '{EpisodeId}' after tweet update. Attempt {Attempt} of {Attempts}.",
                    contentKind, podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id, attempt, MaxSaveAttempts);
                if (attempt < MaxSaveAttempts)
                {
                    await saveBackoff.Delay(attempt);
                }
            }
        }

        return lastFailure;
    }

    private async Task PersistNotTweetable(PodcastEpisode podcastEpisode, string reason)
    {
        // Skip reason alone makes Tweeted true; do not set OldTweeted (that means posted / duplicate-forbidden).
        PromotableTweetSkip.ApplyNotTweetable(podcastEpisode.Episode, reason);

        try
        {
            if (podcastEpisode.IsCataloguePlayable)
            {
                await cataloguePlayableSocialPosts.MarkNotTweetable(podcastEpisode, reason);
            }
            else
            {
                await episodeRepository.Save(podcastEpisode.Episode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to save not-tweetable reason '{Reason}' for podcast-id '{PodcastId}' episode-id '{EpisodeId}'.",
                reason, podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
            throw;
        }
    }
}
