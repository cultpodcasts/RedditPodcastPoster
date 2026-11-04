using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.EntitySearchIndexer;

/// <summary>
/// Whether a catalogue playable must be absent from Azure Search (delete / skip upload).
/// Mirrors <see cref="EpisodeSearchIndexEligibility"/>: when the live parent is in hand, consult its
/// <c>Removed</c> as well as the child's denormalised <c>ParentRemoved</c>.
/// <c>ParentChangeCascade</c> saves children before deleting search documents, so denorm can lag; a curator
/// edit or Index of a child in that window must not push it back into search.
/// </summary>
public static class CataloguePlayableSearchIndexEligibility
{
    /// <summary>
    /// Denormalised-only. Prefer the overloads that take the live parent when it is already loaded
    /// (or cheap to load) so a stale <c>ParentRemoved == false</c> cannot re-upload under a removed parent.
    /// </summary>
    public static bool ShouldExcludeFromSearch(Playable playable) =>
        playable.IsRemoved() || playable.ParentRemoved == true;

    public static bool ShouldExcludeFromSearch(TvShow show, TvShowEpisode episode) =>
        episode.IsRemoved() || episode.ParentRemoved == true || show.IsRemoved();

    public static bool ShouldExcludeFromSearch(NewsOrganisation organisation, NewsReport report) =>
        report.IsRemoved() || report.ParentRemoved == true || organisation.IsRemoved();

    public static bool ShouldExcludeFromSearch(Film film) => film.IsRemoved();
}
