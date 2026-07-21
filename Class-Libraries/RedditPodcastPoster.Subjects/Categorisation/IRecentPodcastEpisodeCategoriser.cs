using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Categorisation;

public interface IRecentPodcastEpisodeCategoriser
{
    Task<IList<Guid>> Categorise(IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null);
}