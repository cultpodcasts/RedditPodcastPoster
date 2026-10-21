using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.SocialPosting.Templates;
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

            // Same pieces as the post template, so TV, news and film tweets are found too.
            var fingerprint = SocialPostRemovalFingerprint.For(podcastEpisode);
            var matchingTweets = tweetsResponse.Tweets
                .Where(x => fingerprint.Matches(x.Text, x.ExpandedUrls()))
                .ToArray();
            if (!matchingTweets.Any())
            {
                return RemoveTweetState.NotFound;
            }

            if (matchingTweets.Length > 1)
            {
                logger.LogError(
                    "Multiple tweets ({Count}) found matching episode-id '{EpisodeId}'", matchingTweets.Length, podcastEpisode.Episode.Id);
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
