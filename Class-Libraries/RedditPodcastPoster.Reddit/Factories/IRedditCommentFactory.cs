using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IRedditCommentFactory
{
    string ToComment(PostModel postModel);
}