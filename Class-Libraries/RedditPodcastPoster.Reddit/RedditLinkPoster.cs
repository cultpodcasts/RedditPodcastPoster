using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using Reddit.Exceptions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

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

    public async Task<PostResponse> Post(PostModel postModel)
    {
        var link = postModel.Link;

        if (link == null)
        {
            return new PostResponse(null, false);
        }

        var title = _redditPostTitleFactory.ConstructPostTitle(postModel);
        var post = _redditClient
            .Subreddit(_settings.SubredditName)
            .LinkPost(title, link.ToString());
        var posted = false;
        LinkPost? linkPost = null;
        try
        {
            linkPost = await post.SubmitAsync();
            posted = true;
        }
        catch (RedditAlreadySubmittedException ex)
        {
            _logger.LogError(ex, $"Post already submitted. Link: '{link}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error posting link '{link}' and title '{title}'.");
            throw;
        }

        return new PostResponse(linkPost, posted);
    }
}