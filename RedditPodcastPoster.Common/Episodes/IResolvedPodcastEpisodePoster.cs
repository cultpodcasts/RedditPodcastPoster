using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IResolvedPodcastEpisodePoster
{
    Task<ProcessResponse> PostResolvedPodcastEpisode(ResolvedPodcastEpisode resolvedEpisode);
}