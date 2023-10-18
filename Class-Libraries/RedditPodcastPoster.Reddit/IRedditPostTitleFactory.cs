using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IRedditPostTitleFactory
{
    string ConstructPostTitle(PostModel postModel);
}