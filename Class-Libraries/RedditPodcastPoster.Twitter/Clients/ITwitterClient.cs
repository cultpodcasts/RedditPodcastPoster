using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

public interface ITwitterClient
{
    Task<PostTweetResponse> Send(string tweet);
    Task<GetTweetsResponseWrapper> GetTweets();
    Task<bool> DeleteTweet(Tweet tweet);

    /// <summary>
    /// One page (up to 100) of the account's posts created at or after <paramref name="startTime"/>, newest first.
    /// <see cref="GetTweetsResponseWrapper.NextToken"/> is set when there is another page.
    /// </summary>
    Task<GetTweetsResponseWrapper> GetTimelineSince(DateTimeOffset startTime, string? paginationToken);

    /// <summary>Deletes the post with <paramref name="tweetId"/>. A 404 is <see cref="DeleteTweetState.NotFound"/>.</summary>
    Task<DeleteTweetState> DeleteTweetById(string tweetId);
}
