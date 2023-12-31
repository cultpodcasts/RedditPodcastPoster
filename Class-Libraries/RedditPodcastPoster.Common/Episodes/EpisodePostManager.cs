﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodePostManager : IEpisodePostManager
{
    private readonly ILogger<EpisodePostManager> _logger;
    private readonly IRedditBundleCommentFactory _redditBundleCommentFactory;
    private readonly IRedditEpisodeCommentFactory _redditEpisodeCommentFactory;
    private readonly IRedditLinkPoster _redditLinkPoster;
    private readonly ISubjectRepository _subjectRepository;

    public EpisodePostManager(
        IRedditLinkPoster redditLinkPoster,
        IRedditEpisodeCommentFactory redditEpisodeCommentFactory,
        IRedditBundleCommentFactory redditBundleCommentFactory,
        ISubjectRepository subjectRepository,
        ILogger<EpisodePostManager> logger)
    {
        _redditLinkPoster = redditLinkPoster;
        _redditEpisodeCommentFactory = redditEpisodeCommentFactory;
        _redditBundleCommentFactory = redditBundleCommentFactory;
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<ProcessResponse> Post(PostModel postModel)
    {
        _logger.LogInformation(
            $"{nameof(Post)} Posting '{postModel.EpisodeTitle}' / '{postModel.PodcastName}' published '{postModel.Published:R}' bundled='{postModel.IsBundledPost}'.");
        var result = await PostEpisode(postModel);
        if (result is {Success: false, AlreadyPosted: false})
        {
            return ProcessResponse.Fail(
                $"Could not post episode with id {postModel.Id}. Results-Message:{result.Message}");
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
            if (!result.Posted)
            {
                return RedditPostResult.FailAlreadyPosted();
            }

            if (result.LinkPost != null)
            {
                if (postModel.Subject != null)
                {
                    var subject = await _subjectRepository.GetByName(postModel.Subject);
                    if (subject is {RedditFlairTemplateId: not null})
                    {
                        var flairTemplateId = subject.RedditFlairTemplateId.ToString();
                        result.LinkPost.SetFlair(postModel.Subject, flairTemplateId);
                    }
                    else
                    {
                        if (subject != null)
                        {
                            _logger.LogError(
                                $"No flair-id for subject '{postModel.Subject}' with subject-id '{subject.Id}'.");
                        }
                        else
                        {
                            _logger.LogError($"No persisted subject for '{postModel.Subject}'.");
                        }

                        result.LinkPost.SetFlair(postModel.Subject);
                    }
                }

                string comments;
                if (postModel.IsBundledPost)
                {
                    comments = _redditBundleCommentFactory.Post(postModel);
                }
                else
                {
                    comments = _redditEpisodeCommentFactory.Post(postModel);
                }

                if (!string.IsNullOrWhiteSpace(comments.Trim()))
                {
                    await result.LinkPost.ReplyAsync(comments);
                }
            }
            else
            {
                return RedditPostResult.Fail("No post to reply to.");
            }
        }
        catch (Exception ex)
        {
            return RedditPostResult.Fail(ex.Message);
        }

        return RedditPostResult.Successful();
    }
}