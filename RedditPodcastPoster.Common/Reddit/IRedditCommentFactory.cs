using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditCommentFactory
{
    string Post(PostModel postModel);
}