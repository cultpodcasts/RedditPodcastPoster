using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Options;
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
/// <item>With a stored <c>TweetId</c>: delete by id. NotFound only when X says the post is not found.</item>
/// <item>Without one (posted before the id was stored, or flagged by hand): search the account's posts from the day
/// before release, 100 per page, up to <see cref="MaxTimelinePages"/> pages, and require exactly one match across
/// every page read. Podcast episodes also bound the window with <c>end_time</c> (release + tweet-days + slack), since
/// the hourly job posts them soon after release.</item>
/// </list>
/// <see cref="RemoveTweetState.NotFound"/> (which lets the caller clear Tweeted) is returned only when the whole window
/// up to now was read without a match. A capped read, a window that ends before now, or a match with unread pages left
/// is <see cref="RemoveTweetState.Other"/>, so the caller keeps the item marked tweeted.
/// <para>
/// Cost (X pay-per-use, Nov 2026): delete is $0.010 per request; reading the account's own timeline is $0.001 per post
/// returned. A legacy removal at the cap is about 300 × $0.001 + $0.010 ≈ $0.31; by stored id it is $0.010.
/// </para>
/// </summary>
public class TweetManager(
    ITwitterClient twitterClient,
    ITextSanitiser textSanitiser,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<TweetManager> logger
) : ITweetManager
{
    /// <summary>Most timeline pages (of 100 posts) read for one legacy removal. Each post returned is billed.</summary>
    public const int MaxTimelinePages = 3;

    /// <summary>Days added after the tweet-days posting window when bounding a podcast episode's search.</summary>
    public const int EndTimeSlackDays = 2;

    /// <summary>X does not accept a <c>start_time</c> before this.</summary>
    public static readonly DateTimeOffset EarliestStartTime = new(2010, 11, 6, 0, 0, 1, TimeSpan.Zero);

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
        var endTime = TimelineEnd(podcastEpisode, postingCriteria.Value?.TweetDays ?? 0, DateTimeOffset.UtcNow);

        string? paginationToken = null;
        Tweet? match = null;
        var readWholeWindow = false;
        for (var page = 1; page <= MaxTimelinePages; page++)
        {
            var tweetsResponse = await twitterClient.GetTimelineSince(startTime, endTime, paginationToken);
            if (tweetsResponse.State == GetTweetsState.TooManyRequests)
            {
                return RemoveTweetState.TooManyRequests;
            }

            if (tweetsResponse.State != GetTweetsState.Retrieved || tweetsResponse.Tweets == null)
            {
                return RemoveTweetState.Other;
            }

            foreach (var tweet in tweetsResponse.Tweets.Where(x => fingerprint.Matches(x.Text, x.ExpandedUrls())))
            {
                if (match != null)
                {
                    logger.LogError(
                        "Multiple tweets ('{FirstTweetId}', '{OtherTweetId}', ...) match episode-id '{EpisodeId}'; deleting none.",
                        match.Id, tweet.Id, podcastEpisode.Episode.Id);
                    return RemoveTweetState.Other;
                }

                match = tweet;
            }

            paginationToken = tweetsResponse.NextToken;
            if (paginationToken == null)
            {
                readWholeWindow = true;
                break;
            }
        }

        if (match == null)
        {
            if (!readWholeWindow)
            {
                logger.LogWarning(
                    "No tweet matching episode-id '{EpisodeId}' in the newest {Pages} pages since '{StartTime:O}'; not treating it as gone.",
                    podcastEpisode.Episode.Id, MaxTimelinePages, startTime);
                return RemoveTweetState.Other;
            }

            if (endTime.HasValue)
            {
                logger.LogWarning(
                    "No tweet matching episode-id '{EpisodeId}' between '{StartTime:O}' and '{EndTime:O}'; a later post (e.g. by hand) is possible, so not treating it as gone.",
                    podcastEpisode.Episode.Id, startTime, endTime);
                return RemoveTweetState.Other;
            }

            return RemoveTweetState.NotFound;
        }

        var state = await DeleteMatch(match);
        if (!readWholeWindow && state is RemoveTweetState.Deleted or RemoveTweetState.NotFound)
        {
            // Pages were left unread, so another copy may still be live: keep the item marked tweeted.
            logger.LogWarning(
                "Deleted tweet '{TweetId}' for episode-id '{EpisodeId}', but stopped at {Pages} pages; keeping it marked tweeted in case another copy is live.",
                match.Id, podcastEpisode.Episode.Id, MaxTimelinePages);
            return RemoveTweetState.Other;
        }

        return state;
    }

    private async Task<RemoveTweetState> DeleteMatch(Tweet tweet) =>
        ToRemoveState(await twitterClient.DeleteTweetById(tweet.Id));

    /// <summary>
    /// <c>end_time</c> for a podcast episode: release + <paramref name="tweetDays"/> + <see cref="EndTimeSlackDays"/>.
    /// Null (read up to now) for films, TV and news, when tweet-days is not set, or when that end is not before now.
    /// </summary>
    public static DateTimeOffset? TimelineEnd(PodcastEpisode item, int tweetDays, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsCataloguePlayable || tweetDays <= 0)
        {
            return null;
        }

        var release = DateTime.SpecifyKind(item.Episode.ReleaseUtc, DateTimeKind.Utc);
        if (release > now.UtcDateTime.AddDays(-(tweetDays + EndTimeSlackDays)))
        {
            return null;
        }

        return new DateTimeOffset(release.AddDays(tweetDays + EndTimeSlackDays));
    }

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
