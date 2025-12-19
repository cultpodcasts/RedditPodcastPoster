using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public class TweetPoster(
    IPodcastRepository repository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    ILogger<TweetPoster> logger)
    : ITweetPoster
{
    public async Task<PostTweetResponse> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortUrl);
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
            try
            {
                await repository.Save(podcastEpisode.Podcast);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failure to save podcast with podcast-id '{PodcastId}' to update episode with id '{EpisodeId}'.", podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                throw;
            }

            return tweetStatus;
        }

        return tweetStatus;
    }
}