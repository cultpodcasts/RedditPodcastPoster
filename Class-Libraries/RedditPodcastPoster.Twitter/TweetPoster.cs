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
    public async Task PostTweet(PodcastEpisode podcastEpisode)
    {
        var tweet = await tweetBuilder.BuildTweet(podcastEpisode);
        bool tweeted;
        try
        {
            tweeted = await twitterClient.Send(tweet);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to send tweet for podcast-id '{podcastEpisode.Podcast.Id}' episode-id '{podcastEpisode.Episode.Id}', tweet: '{tweet}'.");
            throw;
        }

        if (tweeted)
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
        }
        else
        {
            var message =
                $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
            logger.LogError(message);
            throw new Exception(message);
        }
    }
}