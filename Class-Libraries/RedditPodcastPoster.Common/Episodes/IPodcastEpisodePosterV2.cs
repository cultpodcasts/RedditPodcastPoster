using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 version of IPodcastEpisodePoster that works with detached episodes via IEpisodeRepository.
/// Accepts PodcastEpisodeV2 with V2 models.
/// </summary>
public interface IPodcastEpisodePosterV2
{
    /// <summary>
    /// Posts a podcast episode (or bundle of episodes) to Reddit and updates the posted status.
    /// </summary>
    Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisodeV2 podcastEpisode,
        bool preferYouTube = false);
}
