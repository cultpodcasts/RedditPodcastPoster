using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IRedditCommentFactory
{
    string Post(PostModel postModel);
}