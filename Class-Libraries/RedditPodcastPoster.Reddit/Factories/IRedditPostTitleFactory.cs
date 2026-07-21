using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IRedditPostTitleFactory
{
    Task<string> ConstructPostTitle(PostModel postModel);
}