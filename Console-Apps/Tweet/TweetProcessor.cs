using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;

namespace Tweet;

public class TweetProcessor(
    IPodcastRepository podcastRepository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    ILogger<TweetProcessor> logger)
{
    public async Task Run(TweetRequest request)
    {
        var podcast = await podcastRepository.GetPodcast(request.PodcastId);
        if (podcast != null)
        {
            var mostRecentEpisode =
                podcast.Episodes
                    .Where(x => x is {Tweeted: false, Ignored: false, Removed: false})
                    .MaxBy(x => x.Release);

            if (mostRecentEpisode != null)
            {
                var podcastEpisode = new PodcastEpisode(podcast, mostRecentEpisode);
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
                        await podcastRepository.Update(podcastEpisode.Podcast);
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
            else
            {
                var message =
                    $"Could not find an episode for podcast '{podcast.Name}' with id: '{podcast.Id}'.";
                logger.LogError(message);
                throw new Exception(message);
            }
        }
        else
        {
            var message =
                $"Could not find an podcast with id: '{request.PodcastId}'.";
            logger.LogError(message);
            throw new Exception(message);
        }
    }
}