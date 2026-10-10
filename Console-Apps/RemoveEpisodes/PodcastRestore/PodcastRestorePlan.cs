using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RemoveEpisodes.PodcastRestore;

/// <summary>
///     What undoing a podcast removal changes. Built from current state only, so it is safe to print
///     in a dry run and idempotent to re-apply.
/// </summary>
/// <remarks>
///     Removing a podcast (Api <c>PodcastUpdateService</c>, <c>removed: true</c>) is a soft delete:
///     it sets <c>podcast.removed = true</c>, stamps <c>parentRemoved = true</c> on every episode,
///     deletes every episode's search document, and deletes every episode's short-URL key.
///     It never touches <c>episode.removed</c>, so episodes removed before the accident stay removed
///     and are excluded from re-indexing and short-URL re-creation.
///     Selection/reporting only: the processor restores the episode projection with
///     <see cref="Episode.SetPodcastProperties" />, the same domain projection removal relies on.
/// </remarks>
public sealed record PodcastRestorePlan(
    Podcast Podcast,
    bool PodcastNeedsUnremove,
    IReadOnlyList<Episode> EpisodesToClearParentRemoved,
    IReadOnlyList<Episode> EpisodesToRepublish,
    IReadOnlyList<Episode> EpisodesLeftRemoved)
{
    public bool HasChanges =>
        PodcastNeedsUnremove || EpisodesToClearParentRemoved.Count > 0 || EpisodesToRepublish.Count > 0;

    public static PodcastRestorePlan Create(Podcast podcast, IEnumerable<Episode> episodes)
    {
        var all = episodes.ToList();
        return new PodcastRestorePlan(
            podcast,
            podcast.IsRemoved(),
            all.Where(e => e.ParentRemoved == true).ToList(),
            all.Where(e => !e.IsRemoved()).ToList(),
            all.Where(e => e.IsRemoved()).ToList());
    }
}
