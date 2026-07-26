using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

public interface IHeroEpisodePromoter
{
    /// <summary>
    /// Best-effort append of newly indexed episode IDs to the edge hero list.
    /// Must not throw in a way that fails indexing — implementations log and swallow.
    /// </summary>
    Task PromoteAsync(IReadOnlyList<Guid> episodeIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Selects newly indexed episode IDs eligible for auto-hero when the podcast is flagged.
/// </summary>
public static class HeroAutoPromoteSelector
{
    public static readonly TimeSpan WeekWindow = TimeSpan.FromDays(7);

    public static IReadOnlyList<Guid> SelectEpisodeIds(
        Podcast podcast,
        IEnumerable<Episode> addedEpisodes,
        DateTime utcNow)
    {
        if (podcast.AlwaysPromoteAsHero != true)
        {
            return [];
        }

        var cutoff = utcNow - WeekWindow;
        return addedEpisodes
            .Where(ep =>
                !ep.Ignored &&
                !ep.Removed &&
                ep.Release >= cutoff)
            .Select(ep => ep.Id)
            .Distinct()
            .ToArray();
    }
}
