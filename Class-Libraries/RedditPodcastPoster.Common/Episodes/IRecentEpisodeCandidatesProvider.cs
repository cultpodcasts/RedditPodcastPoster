using Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Common.Episodes;

public interface IRecentEpisodeCandidatesProvider
{
    Task<IReadOnlyCollection<Episode>> GetRecentActiveEpisodes(DateTime releasedSince);
}
