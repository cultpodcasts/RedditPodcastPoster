using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching.Candidates;

/// <summary>
/// Shared matching step for catalogue lookups: release-band slicing and strategy choice
/// (duration-aware vs release-date) over domain episodes.
/// </summary>
public interface ICatalogueEpisodeMatcher
{
    Episode? Match(
        CatalogueLookup lookup,
        Episode probe,
        IReadOnlyList<Episode> candidates,
        IReadOnlyList<string>? ignoredSubjects,
        Func<Episode, bool>? reducer = null);
}
