using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
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
    public Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode) =>
        RemoveTweet(SocialPostRemovalTarget.From(podcastEpisode));

    public async Task<RemoveTweetState> RemoveTweet(SocialPostRemovalTarget target)
    {
        var tweetsResponse = await twitterClient.GetTweets();
        if (tweetsResponse.State == GetTweetsState.Retrieved)
        {
            if (tweetsResponse.Tweets == null)
            {
                return RemoveTweetState.Other;
            }

            var matchingTweets = tweetsResponse.Tweets.Where(x => target.Matches(x.Text)).ToArray();
            if (!matchingTweets.Any())
            {
                return RemoveTweetState.NotFound;
            }

            if (matchingTweets.Count() > 1)
            {
                logger.LogError(
                    "Multiple tweets ({Count}) found matching episode-id '{EpisodeId}'", matchingTweets.Count(), target.Id);
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
