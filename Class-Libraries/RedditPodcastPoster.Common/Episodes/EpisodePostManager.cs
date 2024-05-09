using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodePostManager(
    IRedditLinkPoster redditLinkPoster,
    IRedditEpisodeCommentFactory redditEpisodeCommentFactory,
    IRedditBundleCommentFactory redditBundleCommentFactory,
    ISubjectRepository subjectRepository,
    ILogger<EpisodePostManager> logger)
    : IEpisodePostManager
{
    public async Task<ProcessResponse> Post(PostModel postModel)
    {
        var result = await PostEpisode(postModel);
        if (result is {Success: false, AlreadyPosted: false})
        {
            return ProcessResponse.Fail(
                $"Could not post episode with id {postModel.Id}. Results-Message:{result.Message}");
        }

        if (result.AlreadyPosted)
        {
            return ProcessResponse.AlreadyPosted(
                $"Reddit reports episode {postModel.Id} already posted. Updated repository.");
        }

        logger.LogInformation($"{nameof(Post)} Posted '{result.Title}' bundled='{postModel.IsBundledPost}'.");

        return ProcessResponse.Successful();
    }

    private async Task<RedditPostResult> PostEpisode(PostModel postModel)
    {
        try
        {
            var result = await redditLinkPoster.Post(postModel);
            if (!result.Posted)
            {
                return RedditPostResult.FailAlreadyPosted();
            }

            if (result.LinkPost != null)
            {
                if (postModel.Subjects.Any())
                {
                    var subjects = await subjectRepository.GetByNames(postModel.Subjects).ToArrayAsync();
                    var redditSubjects = subjects.Where(x => x.RedditFlairTemplateId != null);
                    var subject =
                        redditSubjects.FirstOrDefault(x => x.SubjectType is null or SubjectType.Canonical) ??
                        redditSubjects.FirstOrDefault();
                    if (subject != null)
                    {
                        var flairTemplateId = subject.RedditFlairTemplateId.ToString();
                        result.LinkPost.SetFlair(subject.Name, flairTemplateId);
                    }
                    else
                    {
                        logger.LogError(
                            $"No subject with flair-id for episode with title '{postModel.EpisodeTitle}'.");
                        result.LinkPost.SetFlair(postModel.Subjects.FirstOrDefault());
                    }
                }

                string comments;
                if (postModel.IsBundledPost)
                {
                    comments = redditBundleCommentFactory.ToComment(postModel);
                }
                else
                {
                    comments = redditEpisodeCommentFactory.ToComment(postModel);
                }

                if (!string.IsNullOrWhiteSpace(comments.Trim()))
                {
                    await result.LinkPost.ReplyAsync(comments);
                }

                return RedditPostResult.Successful(result.LinkPost.Title);
            }

            return RedditPostResult.Fail("No post to reply to.");
        }
        catch (Exception ex)
        {
            return RedditPostResult.Fail(ex.Message);
        }
    }
}