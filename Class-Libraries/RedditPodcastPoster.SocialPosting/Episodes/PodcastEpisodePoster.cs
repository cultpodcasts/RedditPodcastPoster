using Microsoft.Extensions.Logging;
using RedditPodcastPoster.SocialPosting.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Episodes;

/// <summary>
/// Reddit episode posting port. Live Reddit.NET posting is retired — skips without
/// marking <c>Posted</c>. Title/comment constructors remain in the Reddit assembly
/// for a future Devvit poster; <c>RunPoster</c> / website post switches stay.
/// </summary>
public class PodcastEpisodePoster(
    ILogger<PodcastEpisodePoster> logger
) : IPodcastEpisodePoster
{
    public Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisode podcastEpisode,
        bool preferYouTube = false)
    {
        logger.LogInformation(
            "Reddit.NET posting is retired; skipping post for episode '{EpisodeId}'. RunPoster/post switches remain for a future Devvit integration.",
            podcastEpisode.Episode.Id);

        return Task.FromResult(ProcessResponse.Successful(
            $"Reddit posting retired; skipped episode '{podcastEpisode.Episode.Id}'."));
    }
}
