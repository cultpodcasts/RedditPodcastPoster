using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IRedditCommentFactory
{
    string ToComment(PostModel postModel);
}