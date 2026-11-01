using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Managers;

/// <summary>
/// Removes the X post made for an item (#1058, #1204).
/// <list type="bullet">
/// <item>With a stored <c>TweetId</c>: delete by id. A 404 is <see cref="RemoveTweetState.NotFound"/> (already gone).</item>
/// <item>Without one (posted before the id was stored, or flagged by hand): search the account's posts since the day
/// before release, 100 per page, up to <see cref="MaxTimelinePages"/> pages, and delete the single match.
/// <see cref="RemoveTweetState.NotFound"/> only when the whole window was read without a match; a window longer than
/// the page cap is <see cref="RemoveTweetState.Other"/>, so the caller keeps the item marked tweeted.</item>
/// </list>
/// </summary>
public class TweetManager(
    ITwitterClient twitterClient,
    ITextSanitiser textSanitiser,
    ILogger<TweetManager> logger
) : ITweetManager
{
    /// <summary>Most timeline pages (of 100 posts) read for one legacy removal. Each page is billed as post reads.</summary>
    public const int MaxTimelinePages = 3;

    /// <summary>X does not accept a <c>start_time</c> before this.</summary>
    private static readonly DateTimeOffset EarliestStartTime = new(2010, 11, 6, 0, 0, 1, TimeSpan.Zero);

    public async Task<RemoveTweetState> RemoveTweet(PodcastEpisode podcastEpisode)
    {
        ArgumentNullException.ThrowIfNull(podcastEpisode);
        var tweetId = podcastEpisode.Episode.TweetId;
        if (!string.IsNullOrWhiteSpace(tweetId))
        {
            return ToRemoveState(await twitterClient.DeleteTweetById(tweetId));
        }

        return await RemoveByTimelineSearch(podcastEpisode);
    }

    private async Task<RemoveTweetState> RemoveByTimelineSearch(PodcastEpisode podcastEpisode)
    {
        // Posts write the sanitised parent name (TweetBuilder), so match on that.
        var postedName = textSanitiser.SanitisePodcastName(podcastEpisode.Podcast.Name);
        var fingerprint = SocialPostRemovalFingerprint.For(podcastEpisode, postedName);
        var startTime = TimelineStart(podcastEpisode.Episode.ReleaseUtc);

        string? paginationToken = null;
        for (var page = 1; page <= MaxTimelinePages; page++)
        {
            var tweetsResponse = await twitterClient.GetTimelineSince(startTime, paginationToken);
            if (tweetsResponse.State == GetTweetsState.TooManyRequests)
            {
                return RemoveTweetState.TooManyRequests;
            }

            if (tweetsResponse.State != GetTweetsState.Retrieved || tweetsResponse.Tweets == null)
            {
                return RemoveTweetState.Other;
            }

            var matchingTweets = tweetsResponse.Tweets
                .Where(x => fingerprint.Matches(x.Text, x.ExpandedUrls()))
                .ToArray();
            if (matchingTweets.Length > 1)
            {
                logger.LogError(
                    "Multiple tweets ({Count}) found matching episode-id '{EpisodeId}'", matchingTweets.Length,
                    podcastEpisode.Episode.Id);
                return RemoveTweetState.Other;
            }

            if (matchingTweets.Length == 1)
            {
                return await DeleteMatch(matchingTweets[0]);
            }

            paginationToken = tweetsResponse.NextToken;
            if (paginationToken == null)
            {
                return RemoveTweetState.NotFound;
            }
        }

        logger.LogWarning(
            "No tweet matching episode-id '{EpisodeId}' in the newest {Pages} pages since '{StartTime:O}'; not treating it as gone.",
            podcastEpisode.Episode.Id, MaxTimelinePages, startTime);
        return RemoveTweetState.Other;
    }

    private async Task<RemoveTweetState> DeleteMatch(Tweet tweet) =>
        ToRemoveState(await twitterClient.DeleteTweetById(tweet.Id));

    public static DateTimeOffset TimelineStart(DateTime releaseUtc)
    {
        // A day early covers a release time later than the post (time zones, year- or date-only releases).
        var release = DateTime.SpecifyKind(releaseUtc, DateTimeKind.Utc);
        var start = release <= EarliestStartTime.UtcDateTime.AddDays(1)
            ? EarliestStartTime
            : new DateTimeOffset(release.AddDays(-1));
        var now = DateTimeOffset.UtcNow;
        return start > now ? now.AddDays(-1) : start;
    }

    private static RemoveTweetState ToRemoveState(DeleteTweetState state) => state switch
    {
        DeleteTweetState.Deleted => RemoveTweetState.Deleted,
        DeleteTweetState.NotFound => RemoveTweetState.NotFound,
        DeleteTweetState.TooManyRequests => RemoveTweetState.TooManyRequests,
        _ => RemoveTweetState.Other
    };
}
