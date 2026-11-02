using Microsoft.Extensions.Logging;
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
    ILogger<TweetPoster> logger)
    : ITweetPoster
{
    public async Task<PostTweetResponse> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl, bool hasShareImage = false)
    {
        var tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortUrl, hasShareImage);
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure to save episode with podcast-id '{PodcastId}' and episode-id '{EpisodeId}' after tweet update.", podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                throw;
            }

            return tweetStatus;
        }

        return tweetStatus;
    }
}
