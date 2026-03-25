using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IRecentEpisodeCandidatesProvider
{
    Task<IReadOnlyCollection<PodcastEpisode>> GetRecentActiveEpisodes(DateTime releasedSince);
}
