namespace RedditPodcastPoster.Twitter.Models;

/// <param name="TweetId">
/// Id of the new post from the <c>POST /2/tweets</c> response. Set only on <see cref="TweetSendStatus.Sent"/>, and
/// null when X's body could not be read (the post was still made).
/// </param>
public record PostTweetResponse(TweetSendStatus TweetSendStatus, string? candidateTweet = null, string? TweetId = null);
