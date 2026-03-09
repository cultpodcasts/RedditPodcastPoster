using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 version of IPodcastEpisodePoster that works with detached episodes via IEpisodeRepository.
/// Handles posting episodes and updating their posted status in the detached repository.
/// </summary>
public interface IPodcastEpisodePosterV2
{
    /// <summary>
    /// Posts a podcast episode (or bundle of episodes) to Reddit and updates the posted status.
    /// </summary>
    /// <param name="podcastEpisode">The podcast episode to post</param>
    /// <param name="preferYouTube">Whether to prefer YouTube links over other services</param>
    /// <returns>The result of the posting operation</returns>
    Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisode podcastEpisode,
        bool preferYouTube = false);
}
