using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

public interface ITwitterClient
{
    Task<PostTweetResponse> Send(string tweet);
    Task<GetTweetsResponseWrapper> GetTweets();
    Task<bool> DeleteTweet(Tweet tweet);
}