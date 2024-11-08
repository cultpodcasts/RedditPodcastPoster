using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Twitter;

public class Tweeter(
    ITweetPoster tweetPoster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    ILogger<Tweeter> logger)
    : ITweeter
{
    public async Task Tweet(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> untweeted;
        try
        {
            logger.LogInformation(
                $"{nameof(Tweeter)}.{nameof(Tweet)}: Exec {nameof(podcastEpisodeProvider.GetUntweetedPodcastEpisodes)} init.");
            untweeted = await podcastEpisodeProvider.GetUntweetedPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
            logger.LogInformation(
                $"{nameof(Tweeter)}.{nameof(Tweet)}: Exec {nameof(podcastEpisodeProvider.GetUntweetedPodcastEpisodes)} complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        if (untweeted.Any())
        {
            var tweeted = false;
            var tooManyRequests = false;
            foreach (var podcastEpisode in untweeted)
            {
                if (tweeted || tooManyRequests)
                {
                    break;
                }

                try
                {
                    logger.LogInformation(
                        $"{nameof(Tweeter)}.{nameof(Tweet)}: Exec {nameof(tweetPoster.PostTweet)} init.");
                    var tweetStatus = await tweetPoster.PostTweet(podcastEpisode);
                    logger.LogInformation(
                        $"{nameof(Tweeter)}.{nameof(Tweet)}: Exec {nameof(tweetPoster.PostTweet)} complete.");
                    tweeted = tweetStatus == TweetSendStatus.Sent;
                    tooManyRequests = tweetStatus == TweetSendStatus.TooManyRequests;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        $"Unable to tweet episode with id '{podcastEpisode.Episode.Id}' with title '{podcastEpisode.Episode.Title}' from podcast with id '{podcastEpisode.Podcast.Id}' and name '{podcastEpisode.Podcast.Name}'.");
                }
            }
        }
    }
}