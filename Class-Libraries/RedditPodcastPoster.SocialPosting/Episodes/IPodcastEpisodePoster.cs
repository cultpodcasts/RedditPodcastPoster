using RedditPodcastPoster.SocialPosting.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Episodes;

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

