using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IRedditPostTitleFactory
{
    Task<string> ConstructPostTitle(PostModel postModel);
}