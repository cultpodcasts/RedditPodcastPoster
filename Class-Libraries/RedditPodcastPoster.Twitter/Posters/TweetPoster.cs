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
            podcastEpisode.Episode.Tweeted = true;
            // The id is only known for a new post. A duplicate-content refusal keeps any id already stored.
            if (tweetStatus.TweetSendStatus == TweetSendStatus.Sent && !string.IsNullOrWhiteSpace(tweetStatus.TweetId))
            {
                var previousTweetId = podcastEpisode.Episode.TweetId;
                if (!string.IsNullOrWhiteSpace(previousTweetId) && previousTweetId != tweetStatus.TweetId)
                {
                    // Posting an already-tweeted item (#1205). Log the older id so that post can still be found.
                    logger.LogWarning(
                        "Episode-id '{EpisodeId}' of podcast-id '{PodcastId}' already had tweet-id '{PreviousTweetId}'; replacing it with new tweet-id '{TweetId}'. The older post may still be live.",
                        podcastEpisode.Episode.Id, podcastEpisode.Podcast.Id, previousTweetId, tweetStatus.TweetId);
                }

                podcastEpisode.Episode.TweetId = tweetStatus.TweetId;
            }

            try
            {
                if (podcastEpisode.IsCataloguePlayable)
                {
                    await cataloguePlayableSocialPosts.MarkTweeted(podcastEpisode, podcastEpisode.Episode.TweetId);
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
