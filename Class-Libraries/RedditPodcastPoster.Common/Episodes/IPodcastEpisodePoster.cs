using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// Posts podcast episodes backed by detached `IEpisodeRepository` entities.
/// Accepts `PodcastEpisodeV2` values.
/// </summary>
public interface IPodcastEpisodePoster
{
    Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisodeV2 podcastEpisode,
        bool preferYouTube = false);
}
