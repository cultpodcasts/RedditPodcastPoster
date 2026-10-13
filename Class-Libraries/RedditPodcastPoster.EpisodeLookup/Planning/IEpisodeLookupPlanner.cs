using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;

namespace RedditPodcastPoster.EpisodeLookup;

/// <summary>
/// Chooses the ordered strategy chain for one lookup from flow, release age, window, show size and platform.
/// Shared by every use case: read <c>RedditPodcastPoster.EpisodeLookup/README.md</c> before changing it.
/// </summary>
public interface IEpisodeLookupPlanner
{
    IReadOnlyList<CatalogueLookupStep> Plan(
        CatalogueLookup lookup,
        bool hasShow,
        int? showEpisodeCount,
        string platform);
}
