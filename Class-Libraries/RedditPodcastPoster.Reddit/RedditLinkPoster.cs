using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using Reddit.Exceptions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class RedditLinkPoster(
    IRedditPostTitleFactory redditPostTitleFactory,
    RedditClient redditClient,
    IOptions<SubredditSettings> settings,
    ILogger<RedditLinkPoster> logger)
    : IRedditLinkPoster
{
    private readonly SubredditSettings _settings = settings.Value;

    public async Task<PostResponse> Post(PostModel postModel)
    {
        var link = postModel.Link;

        if (link == null)
        {
            return new PostResponse(null, false);
        }

        var title = redditPostTitleFactory.ConstructPostTitle(postModel);
        var post = redditClient
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
            logger.LogError(ex, $"Post already submitted. Link: '{link}' and title '{title}'.");
        }
        catch (RedditForbiddenException ex)
        {
            var statusDescription = (string) (ex.Data["StatusDescription"] ?? string.Empty);
            var content = (string) (ex.Data["Content"] ?? string.Empty);

            logger.LogError(ex,
                $"Forbidden from posting link '{link}' and title '{title}'. Status: '{statusDescription}', content: '{content}'.");

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error posting link '{link}' and title '{title}'.");
            throw;
        }

        return new PostResponse(linkPost, posted);
    }
}