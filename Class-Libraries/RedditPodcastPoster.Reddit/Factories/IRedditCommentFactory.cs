using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IRedditCommentFactory
{
    string ToComment(PostModel postModel);
}