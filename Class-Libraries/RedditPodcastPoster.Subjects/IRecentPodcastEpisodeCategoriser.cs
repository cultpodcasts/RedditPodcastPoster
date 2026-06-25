using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface IRecentPodcastEpisodeCategoriser
{
    Task<IList<Guid>> Categorise(IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);
}