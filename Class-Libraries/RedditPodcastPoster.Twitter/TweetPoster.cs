using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public class TweetPoster(
    IEpisodeRepository episodeRepository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    ILogger<TweetPoster> logger)
    : ITweetPoster
{
    public async Task<PostTweetResponse> PostTweet(PodcastEpisodeV2 podcastEpisode, Uri? shortUrl)
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
                await episodeRepository.Save(podcastEpisode.Episode);
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