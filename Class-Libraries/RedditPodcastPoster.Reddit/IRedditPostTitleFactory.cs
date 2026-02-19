using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IRedditPostTitleFactory
{
    Task<string> ConstructPostTitle(PostModel postModel);
}