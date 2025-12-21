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

        untweeted = untweeted.ToArray();
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
                    tweeted = tweetStatus.TweetSendStatus == TweetSendStatus.Sent;
                    tooManyRequests = tweetStatus.TweetSendStatus == TweetSendStatus.TooManyRequests;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Unable to tweet episode with id '{EpisodeId}' with title '{EpisodeTitle}' from podcast with id '{PodcastId}' and name '{PodcastName}'.", podcastEpisode.Episode.Id, podcastEpisode.Episode.Title, podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
                }
            }
        }
    }
}