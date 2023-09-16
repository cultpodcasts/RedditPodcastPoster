using Microsoft.Extensions.Logging;
using Reddit.Exceptions;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Reddit;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodePostManager : IEpisodePostManager
{
    private readonly ILogger<EpisodePostManager> _logger;
    private readonly IRedditBundleCommentFactory _redditBundleCommentFactory;
    private readonly IRedditLinkPoster _redditLinkPoster;
    private readonly IRedditEpisodeCommentFactory _redditEpisodeCommentFactory;

    public EpisodePostManager(
        IRedditLinkPoster redditLinkPoster,
        IRedditEpisodeCommentFactory redditEpisodeCommentFactory,
        IRedditBundleCommentFactory redditBundleCommentFactory,
        ILogger<EpisodePostManager> logger)
    {
        _redditLinkPoster = redditLinkPoster;
        _redditEpisodeCommentFactory = redditEpisodeCommentFactory;
        _redditBundleCommentFactory = redditBundleCommentFactory;
        _logger = logger;
    }

    public async Task<ProcessResponse> Post(PostModel postModel)
    {
        _logger.LogInformation($"{nameof(Post)} Posting '{postModel.EpisodeTitle}' / '{postModel.PodcastName}' bundled='{postModel.IsBundledPost}'.");
        var result = await PostEpisode(postModel);
        if (result is {Success: false, AlreadyPosted: false})
        {
            return ProcessResponse.Fail(
                $"Could not post episode with id {postModel.Id}. Result-Message:{result.Message}");
        }

        if (result.AlreadyPosted)
        {
            return ProcessResponse.Fail(
                $"Reddit reports episode {postModel.Id} already posted. Updated repository.");
        }

        return ProcessResponse.Successful();
    }

    private async Task<RedditPostResult> PostEpisode(PostModel postModel)
    {
        try
        {
            var result = await _redditLinkPoster.Post(postModel);
            if (result != null)
            {
                string comments;
                if (postModel.IsBundledPost)
                {
                    comments= _redditBundleCommentFactory.Post(postModel);
                }
                else
                {
                    comments= _redditEpisodeCommentFactory.Post(postModel);
                }
                if (!string.IsNullOrWhiteSpace(comments.Trim()))
                {
                    await result.ReplyAsync(comments);
                }
            }
            else
            {
                return RedditPostResult.Fail("No Url to post");
            }
        }
        catch (RedditAlreadySubmittedException)
        {
            return RedditPostResult.FailAlreadyPosted();
        }
        catch (Exception ex)
        {
            return RedditPostResult.Fail(ex.Message);
        }

        return RedditPostResult.Successful();
    }
}