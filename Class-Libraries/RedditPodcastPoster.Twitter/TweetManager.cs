using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public class TweetManager(
    ITwitterClient twitterClient,
    ILogger<TweetManager> logger
) : ITweetManager
{
    public async Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode)
    {
        var tweetsResponse = await twitterClient.GetTweets();
        if (tweetsResponse.State == GetTweetsState.Retrieved)
        {
            var matchingTweets = tweetsResponse.Tweets.Where(
                x => x.Text.Contains(podcastEpisode.Podcast.Name) &&
                     x.Text.Contains(podcastEpisode.Episode.Length.ToString(TweetBuilder.LengthFormat,
                         CultureInfo.InvariantCulture)) &&
                     x.Text.Contains(podcastEpisode.Episode.Release.ToString(TweetBuilder.ReleaseFormat))
            );
            if (!matchingTweets.Any())
            {
                return RemoveTweetState.NotFound;
            }

            if (matchingTweets.Count() > 1)
            {
                logger.LogError(
                    $"Multiple tweets ({matchingTweets.Count()}) found matching episode-id '{podcastEpisode.Episode.Id}'");
                return RemoveTweetState.Other;
            }

            var deleted = await twitterClient.DeleteTweet(matchingTweets.Single());
            if (deleted)
            {
                return RemoveTweetState.Deleted;
            }

            return RemoveTweetState.Other;
        }

        if (tweetsResponse.State == GetTweetsState.TooManyRequests)
        {
            return RemoveTweetState.TooManyRequests;
        }

        return RemoveTweetState.Other;
    }
}