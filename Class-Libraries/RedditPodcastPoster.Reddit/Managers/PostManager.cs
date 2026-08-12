using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Reddit.Managers;

/// <summary>
/// Live Reddit un-post / flair updates retired with Reddit.NET.
/// Cosmos <c>posted</c> clearing remains in EpisodeUpdateService.
/// </summary>
public class PostManager(ILogger<PostManager> logger) : IPostManager
{
    public Task RemoveEpisodePost(PodcastEpisode podcastEpisode)
    {
        logger.LogInformation(
            "Reddit.NET un-post is retired; skipping live Reddit delete for podcast '{PodcastId}' episode '{EpisodeId}'.",
            podcastEpisode.Podcast.Id,
            podcastEpisode.Episode.Id);
        return Task.CompletedTask;
    }

    public Task UpdateFlare(PodcastEpisode podcastEpisode)
    {
        logger.LogInformation(
            "Reddit.NET flair update is retired; skipping live Reddit flair for podcast '{PodcastId}' episode '{EpisodeId}'.",
            podcastEpisode.Podcast.Id,
            podcastEpisode.Episode.Id);
        return Task.CompletedTask;
    }
}
