using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

public interface ITwitterClient
{
    Task<PostTweetResponse> Send(string tweet, CancellationToken cancellationToken = default);
    Task<GetTweetsResponseWrapper> GetTweets(CancellationToken cancellationToken = default);
    Task<bool> DeleteTweet(Tweet tweet, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page (up to 100) of the account's posts created at or after <paramref name="startTime"/> (and before
    /// <paramref name="endTime"/> when given), newest first.
    /// <see cref="GetTweetsResponseWrapper.NextToken"/> is set when there is another page.
    /// </summary>
    Task<GetTweetsResponseWrapper> GetTimelineSince(DateTimeOffset startTime, DateTimeOffset? endTime,
        string? paginationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the post with <paramref name="tweetId"/>. <see cref="DeleteTweetState.NotFound"/> only when X reports
    /// the post as not found.
    /// </summary>
    Task<DeleteTweetState> DeleteTweetById(string tweetId, CancellationToken cancellationToken = default);
}
