using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IRecentEpisodeCandidatesProvider
{
    Task<IList<PodcastEpisode>> GetRecentActiveEpisodes(DateTime releasedSince);

    /// <summary>
    /// Gets all recent episodes from cache, including ignored and removed ones.
    /// Handles cache initialization and retrieval without filtering.
    /// </summary>
    Task<IReadOnlyCollection<PodcastEpisode>> GetEpisodes(DateTime releasedSince);
}