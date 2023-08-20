using Reddit.Controllers;
using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditCommentPoster
{
    Task Post(PostModel postModel, LinkPost result);
}