using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IRedditLinkPoster
{
    Task<PostResponse> Post(PostModel postModel);
}