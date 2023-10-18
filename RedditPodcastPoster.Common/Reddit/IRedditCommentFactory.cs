using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditCommentFactory
{
    string Post(PostModel postModel);
}