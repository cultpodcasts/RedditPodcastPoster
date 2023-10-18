using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditLinkPoster
{
    Task<LinkPost?> Post(PostModel postModel);
}