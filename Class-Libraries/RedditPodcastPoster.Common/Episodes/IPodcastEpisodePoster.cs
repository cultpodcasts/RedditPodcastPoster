using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodePoster
{
    Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisodeV2 podcastEpisode,
        bool preferYouTube = false);
}