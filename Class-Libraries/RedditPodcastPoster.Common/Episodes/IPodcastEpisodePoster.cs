using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Posts podcast episodes backed by detached `IEpisodeRepository` entities.
/// Accepts `PodcastEpisode` values.
/// </summary>
public interface IPodcastEpisodePoster
{
    Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisode podcastEpisode,
        bool preferYouTube = false);
}

