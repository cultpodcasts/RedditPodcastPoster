using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodeProcessor
{
    Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since,
        int? maxPosts,
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);
}