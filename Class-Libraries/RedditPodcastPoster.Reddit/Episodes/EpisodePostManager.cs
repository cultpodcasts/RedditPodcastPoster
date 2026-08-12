using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Models;

namespace RedditPodcastPoster.Reddit.Episodes;

/// <summary>
/// Reddit.NET posting retired. Keeps the SocialPosting port so
/// <c>activities:RunPoster</c> / publish <c>post</c> remain wired for a future Devvit poster.
/// </summary>
public class EpisodePostManager(ILogger<EpisodePostManager> logger) : IEpisodePostManager
{
    public Task<ProcessResponse> Post(PostModel postModel)
    {
        logger.LogInformation(
            "Reddit.NET posting is retired; skipping post for episode '{EpisodeId}'. RunPoster/post switches remain for a future Devvit integration.",
            postModel.Id);
        return Task.FromResult(ProcessResponse.Fail(
            $"Reddit.NET posting retired; episode '{postModel.Id}' was not posted."));
    }
}
