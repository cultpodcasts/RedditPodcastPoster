using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Managers;

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
            if (tweetsResponse.Tweets == null)
            {
                return RemoveTweetState.Other;
            }

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
                    "Multiple tweets ({Count}) found matching episode-id '{EpisodeId}'", matchingTweets.Count(), podcastEpisode.Episode.Id);
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
