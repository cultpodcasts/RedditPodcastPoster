using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Twitter.Managers;
using RedditPodcastPoster.Twitter.Models;

namespace Api.Services.Catalogue;

/// <summary>Same order, result mapping and error handling as the un-tweet / un-Bluesky steps in <c>EpisodeUpdateService</c>.</summary>
public class PlayableSocialRetraction(
    ITweetManager tweetManager,
    IBlueskyPostManager blueskyPostManager,
    ILogger<PlayableSocialRetraction> logger) : IPlayableSocialRetraction
{
    public async Task<bool?> RemoveBlueskyPost(PostStatusChange change, IPromotable item, SocialPostRemovalTarget target)
    {
        if (!change.UnBluesky)
        {
            return null;
        }

        RemovePostState result;
        try
        {
            result = await blueskyPostManager.RemovePost(target);
            if (result != RemovePostState.Deleted)
            {
                logger.LogWarning("Failure to delete bluesky-post. Result= {removeBlueskyPostResult}.", result);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error using bluesky-post-manager to remove post for item with id '{id}'.", target.Id);
            result = RemovePostState.Other;
        }

        var gone = result is RemovePostState.Deleted or RemovePostState.NotFound;
        if (gone)
        {
            item.ClearBlueskyPostState();
        }

        return gone;
    }

    public async Task<bool?> RemoveTweet(PostStatusChange change, SocialPostRemovalTarget target)
    {
        if (!change.UnTweet)
        {
            return null;
        }

        RemoveTweetState result;
        try
        {
            result = await tweetManager.RemoveTweet(target);
            if (result != RemoveTweetState.Deleted)
            {
                logger.LogWarning("Failure to delete tweet. Result= {removeTweetResult}.", result);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error using tweet-manager to remove tweet for item with id '{id}'.", target.Id);
            result = RemoveTweetState.Other;
        }

        return result == RemoveTweetState.Deleted;
    }
}
