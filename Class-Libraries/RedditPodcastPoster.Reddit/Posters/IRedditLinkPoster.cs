using RedditPodcastPoster.Models;
using RedditPodcastPoster.Reddit.Models;

namespace RedditPodcastPoster.Reddit.Posters;

public interface IRedditLinkPoster
{
    Task<PostResponse> Post(PostModel postModel);
}