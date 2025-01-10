namespace RedditPodcastPoster.Twitter.Models;

public record PostTweetResponse(TweetSendStatus TweetSendStatus, string? candidateTweet=null);
