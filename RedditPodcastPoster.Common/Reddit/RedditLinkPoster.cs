using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Reddit;

public class RedditLinkPoster : IRedditLinkPoster
{
    private readonly ILogger<RedditLinkPoster> _logger;
    private readonly RedditClient _redditClient;
    private readonly IRedditPostTitleFactory _redditPostTitleFactory;
    private readonly SubredditSettings _settings;

    public RedditLinkPoster(
        IRedditPostTitleFactory redditPostTitleFactory,
        RedditClient redditClient,
        IOptions<SubredditSettings> settings,
        ILogger<RedditLinkPoster> logger)
    {
        _redditPostTitleFactory = redditPostTitleFactory;
        _redditClient = redditClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<LinkPost?> Post(PostModel postModel)
    {
        var link = postModel.Link;

        if (link == null)
        {
            return null;
        }

        var title = _redditPostTitleFactory.ConstructPostTitle(postModel);
        var post = _redditClient
            .Subreddit(_settings.SubredditName)
            .LinkPost(title,
                link.ToString());
        return await post.SubmitAsync();
    }
}