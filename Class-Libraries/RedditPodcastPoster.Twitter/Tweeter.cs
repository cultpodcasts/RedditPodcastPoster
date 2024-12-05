using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlShortening;

namespace RedditPodcastPoster.Twitter;

public class Tweeter(
    ITweetPoster tweetPoster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
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
            untweeted = await podcastEpisodeProvider.GetUntweetedPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
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
                    var shortnerResult = await shortnerService.Write(podcastEpisode);
                    if (!shortnerResult.Success)
                    {
                        logger.LogError("Unsuccessful shortening-url.");
                    }

                    var tweetStatus = await tweetPoster.PostTweet(podcastEpisode, shortnerResult.Url);
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