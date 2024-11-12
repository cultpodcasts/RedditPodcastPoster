using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Twitter;

public class TweetPoster(
    IPodcastRepository repository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    ILogger<TweetPoster> logger)
    : ITweetPoster
{
    public async Task<TweetSendStatus> PostTweet(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortUrl);
        TweetSendStatus tweetStatus;
        try
        {
            tweetStatus = await twitterClient.Send(tweet);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to send tweet for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', tweet: '{tweet}'.");
            throw;
        }

        if (tweetStatus is TweetSendStatus.Sent or TweetSendStatus.DuplicateForbidden)
        {
            podcastEpisode.Episode.Tweeted = true;
            try
            {
                await repository.Update(podcastEpisode.Podcast);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    $"Failure to save podcast with podcast-id '{podcastEpisode.Podcast.Id}' to update episode with id '{podcastEpisode.Episode.Id}'.");
                throw;
            }

            return tweetStatus;
        }

        return tweetStatus;
    }
}