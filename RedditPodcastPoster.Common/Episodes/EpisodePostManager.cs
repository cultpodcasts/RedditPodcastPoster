using Microsoft.Extensions.Logging;
using Reddit.Exceptions;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Reddit;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodePostManager : IEpisodePostManager
{
    private readonly ILogger<EpisodePostManager> _logger;
    private readonly IRedditBundleCommentPoster _redditBundleCommentPoster;
    private readonly IRedditLinkPoster _redditLinkPoster;
    private readonly IRedditEpisodeCommentPoster _redditEpisodeCommentPoster;

    public EpisodePostManager(
        IRedditLinkPoster redditLinkPoster,
        IRedditEpisodeCommentPoster redditEpisodeCommentPoster,
        IRedditBundleCommentPoster redditBundleCommentPoster,
        ILogger<EpisodePostManager> logger)
    {
        _redditLinkPoster = redditLinkPoster;
        _redditEpisodeCommentPoster = redditEpisodeCommentPoster;
        _redditBundleCommentPoster = redditBundleCommentPoster;
        _logger = logger;
    }

    public async Task<ProcessResponse> Post(PostModel postModel)
    {
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
                if (postModel.IsBundledPost)
                {
                    await _redditBundleCommentPoster.Post(postModel, result);
                }
                else
                {
                    await _redditEpisodeCommentPoster.Post(postModel, result);
                }
            }
            else
            {
                return RedditPostResult.Fail("No Url to post");
            }
        }
        catch (RedditAlreadySubmittedException submittedException)
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