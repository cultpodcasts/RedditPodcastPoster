using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IPodcastEpisodePoster
{
    Task<ProcessResponse> PostPodcastEpisode(PodcastEpisode podcastEpisode);
}