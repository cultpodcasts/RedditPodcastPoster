using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Logging;
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
/// Why a newly created/indexed episode was not auto-appended to heroes.
/// </summary>
public enum HeroAutoPromoteSkipReason
{
    None = 0,
    FlagOff,
    Ignored,
    Removed,
    OutsideWeekWindow,
    NotCreated
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
        return addedEpisodes
            .Where(ep => GetSkipReason(podcast, ep, utcNow) == HeroAutoPromoteSkipReason.None)
            .Select(ep => ep.Id)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Explains why an episode would not be auto-promoted (or <see cref="HeroAutoPromoteSkipReason.None"/> if eligible).
    /// </summary>
    public static HeroAutoPromoteSkipReason GetSkipReason(
        Podcast podcast,
        Episode episode,
        DateTime utcNow)
    {
        if (podcast.AlwaysPromoteAsHero != true)
        {
            return HeroAutoPromoteSkipReason.FlagOff;
        }

        if (episode.Ignored)
        {
            return HeroAutoPromoteSkipReason.Ignored;
        }

        if (episode.Removed)
        {
            return HeroAutoPromoteSkipReason.Removed;
        }

        var cutoff = utcNow - WeekWindow;
        if (episode.Release < cutoff)
        {
            return HeroAutoPromoteSkipReason.OutsideWeekWindow;
        }

        return HeroAutoPromoteSkipReason.None;
    }

    public static DateTime GetCutoff(DateTime utcNow) => utcNow - WeekWindow;
}

/// <summary>
/// Stable Warning-level diagnostics for hero auto-promote (Information is sampled in production).
/// Prefix <c>Hero auto-promote</c> is the App Insights filter key.
/// </summary>
public static class HeroAutoPromoteLogger
{
    public const string MessagePrefix = "Hero auto-promote:";

    public static void LogAttempt(
        ILogger logger,
        EpisodeCreationSource creationSource,
        Guid podcastId,
        IReadOnlyList<Guid> episodeIds)
    {
        logger.LogWarning(
            "Hero auto-promote: source={CreationSource}, podcastId={PodcastId}, episodeIds={EpisodeIds}.",
            creationSource,
            podcastId,
            string.Join(',', episodeIds));
    }

    public static void LogSkipped(
        ILogger logger,
        HeroAutoPromoteSkipReason reason,
        Guid episodeId,
        Guid podcastId,
        bool? alwaysPromoteAsHero,
        DateTime? release = null,
        DateTime? cutoff = null,
        string? episodeResult = null)
    {
        logger.LogWarning(
            "Hero auto-promote: skipped reason={SkipReason}, episodeId={EpisodeId}, podcastId={PodcastId}, episodeResult={EpisodeResult}, alwaysPromoteAsHero={AlwaysPromoteAsHero}, release={Release}, cutoff={Cutoff}.",
            reason,
            episodeId,
            podcastId,
            episodeResult,
            alwaysPromoteAsHero,
            release,
            cutoff);
    }
}
