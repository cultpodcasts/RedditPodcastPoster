using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

public interface ITwitterClient
{
    Task<PostTweetResponse> Send(string tweet, CancellationToken cancellationToken = default);
    Task<GetTweetsResponseWrapper> GetTweets(CancellationToken cancellationToken = default);
    Task<bool> DeleteTweet(Tweet tweet, CancellationToken cancellationToken = default);
}
