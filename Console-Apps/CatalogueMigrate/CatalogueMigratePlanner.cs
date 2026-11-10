using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Identify = RedditPodcastPoster.UrlSubmission.Migration.CatalogueMigrateIdentify;

namespace CatalogueMigrate;

/// <summary>
/// One podcast's move. Dry-run and apply use this same plan, so the dry-run log is the apply plan.
/// </summary>
public sealed record CatalogueMigrateUnit(
    Podcast Podcast,
    string Kind,
    CatalogueMigrateMovePlan Plan,
    IReadOnlyList<Episode> MoveEpisodes,
    IReadOnlyList<Episode> SearchEpisodes,
    bool Targeted,
    string? ApplyBlockReason)
{
    public bool ApplyEligible => Plan.Accepted && ApplyBlockReason is null;

    /// <summary>Removed episodes move too (still removed) so no orphan rows stay in Episodes.</summary>
    public int RemovedEpisodeCount => MoveEpisodes.Count(episode => episode.Removed);
}

public static class CatalogueMigratePlanner
{
    /// <summary>
    /// S-008: News apply moves only a podcast a curator names with <c>--podcast-id</c>. The hard-coded
    /// 299-name allowlist was retired (#1359); identify heuristics still find candidates in a dry-run scan.
    /// </summary>
    public const string NewsRequiresPodcastId =
        "S-008: News apply moves only a curator-named podcast. Re-run with --podcast-id for this podcast.";

    public const string PartialNews =
        "Only some episodes are News. A curator must split this podcast before News apply.";

    /// <summary>
    /// Only Film refuses an auto-indexed podcast. A TV show or news organisation keeps the podcast's discovery
    /// settings (<see cref="CatalogueIndexingSource"/>) and the hourly indexer keeps indexing it (#1050), so
    /// auto-indexed News and TV podcasts move like any other.
    /// </summary>
    public const string FilmAutoIndexed =
        "Film podcast is auto-indexed (IndexAllEpisodes or EpisodeIncludeTitleRegex). A Film has no parent to keep " +
        "indexing, so moving it would stop new episodes. A curator must turn auto-index off first.";

    /// <summary>
    /// Apply-time only (never in a dry run): an auto-indexed podcast is not moved while the hourly indexer may be
    /// indexing it, because a pass that read the podcast before the move would save it again after the move and
    /// re-create the deleted row. See <see cref="CatalogueMigrateIndexerWindow"/>.
    /// </summary>
    public const string IndexerMayBeRunning =
        "Podcast is auto-indexed and the hourly indexer may be running (outside minutes :15-:54 of the hour). " +
        "Re-run between :15 and :54.";

    public const string FilmHasOtherEpisodes =
        "Film podcast has other (removed) episodes. A curator must clear them before Film apply.";

    public const string ServiceIdsWouldBeDropped =
        "The TV show / news organisation would not keep the podcast's YouTube / Spotify / Apple ids, so submits and " +
        "the indexer would stop matching it. Merge and deploy PR #1050 (parents keep indexing ids) before apply.";

    /// <summary>
    /// Prefix of the apply-block reason when
    /// <see cref="CatalogueMigrateServiceIds.YouTubePlaylistOwnershipRefusal"/> refuses the pair (#1178).
    /// Full text comes from <see cref="CatalogueMigrateServiceIds.OwnershipBlockReason"/>.
    /// </summary>
    public const string YouTubePlaylistOwnershipRefusedPrefix = "YouTube playlist ownership refused";

    public const string ParentNameExists =
        "Another TV show / news organisation already has this name (or fileKey). Apply would create a duplicate " +
        "parent. A curator must merge or rename first.";

    /// <summary>
    /// Null when the podcast is not a candidate for this kind (same filter as identify-scan dry-run).
    /// </summary>
    public static CatalogueMigrateUnit? PlanUnit(
        Podcast podcast,
        IReadOnlyList<Episode> allEpisodes,
        string kind,
        bool targeted)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(allEpisodes);
        if (podcast.Removed == true)
        {
            return null;
        }

        var active = allEpisodes.Where(episode => !episode.Removed).ToList();
        var suggestion = Identify.FromPodcast(podcast, active);
        var planEpisodes = (IReadOnlyList<Episode>) active;
        string? block = null;

        switch (kind)
        {
            case SubmitClassification.NewsReport:
            {
                var youTubeOnlyNews = Identify.IsYouTubeOnlyNewsOrganisation(podcast, active);
                var newsEpisodes = active.Where(Identify.IsNewsReportEpisode).ToList();
                var acceptedNews = suggestion.ContentKind == SubmitClassification.NewsReport &&
                                   !suggestion.RequiresCurator;
                // Identify marks mixed news/non-news as RequiresCurator Episode, which used to drop the
                // podcast from the News scan. Plan those with PartialNews so an untargeted scan shows
                // the split reason instead of a blank line or only NewsRequiresPodcastId.
                var mixedNews = newsEpisodes.Count > 0 && newsEpisodes.Count != active.Count;
                if (!acceptedNews && !mixedNews)
                {
                    return null;
                }

                planEpisodes = youTubeOnlyNews ? active : newsEpisodes;
                if (planEpisodes.Count == 0)
                {
                    return null;
                }

                // Prefer PartialNews over the untargeted S-008 gate so a scan shows the useful reason.
                // NewsRequiresPodcastId is applied last in CatalogueMigrateProcessor after playlist /
                // duplicate-parent checks, so those reasons can surface on an untargeted scan too.
                if (planEpisodes.Count != active.Count)
                {
                    block = PartialNews;
                }

                break;
            }
            case SubmitClassification.TvShowEpisode when !targeted:
                if (suggestion.ContentKind != SubmitClassification.TvShowEpisode)
                {
                    return null;
                }

                break;
            case SubmitClassification.Film:
                if (allEpisodes.Count != active.Count)
                {
                    block = FilmHasOtherEpisodes;
                }
                else if (IsAutoIndexed(podcast))
                {
                    block = FilmAutoIndexed;
                }

                break;
        }

        if (block is null && DestinationParent(podcast, kind) is { } parent &&
            CatalogueMigrateServiceIds.Dropped(podcast, parent).Count > 0)
        {
            block = ServiceIdsWouldBeDropped;
        }

        var plan = CatalogueMigrateMover.Plan(podcast, planEpisodes, kind);
        return new CatalogueMigrateUnit(
            podcast,
            kind,
            plan,
            MoveEpisodes: allEpisodes,
            SearchEpisodes: planEpisodes,
            targeted,
            block);
    }

    /// <summary>Same rule as the hourly indexer: index all episodes, or only titles matching the include regex.</summary>
    public static bool IsAutoIndexed(Podcast podcast) =>
        podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex);

    /// <summary>The TV show / news organisation the writer would store for this podcast; null for Film.</summary>
    public static Publisher? DestinationParent(Podcast podcast, string kind) => kind switch
    {
        SubmitClassification.NewsReport => CatalogueParentKindMapper.ToNewsOrganisation(podcast),
        SubmitClassification.TvShowEpisode => CatalogueParentKindMapper.ToTvShow(podcast),
        _ => null
    };
}
