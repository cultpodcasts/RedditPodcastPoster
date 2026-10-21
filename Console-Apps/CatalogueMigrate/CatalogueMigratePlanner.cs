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
    bool OnAllowlist,
    string? ApplyBlockReason)
{
    public bool ApplyEligible => Plan.Accepted && ApplyBlockReason is null;

    /// <summary>Removed episodes move too (still removed) so no orphan rows stay in Episodes.</summary>
    public int RemovedEpisodeCount => MoveEpisodes.Count(episode => episode.Removed);
}

public static class CatalogueMigratePlanner
{
    public const string NotOnAllowlist =
        "S-008: publisher is not on CatalogueNewsOrganisationAllowlist, so News apply is refused.";

    public const string PartialNews =
        "Only some episodes are News. A curator must split this podcast before News apply.";

    public const string AutoIndexed =
        "Podcast is auto-indexed (IndexAllEpisodes or EpisodeIncludeTitleRegex). Film, TV, and News parents are not indexed, " +
        "so moving it would stop new episodes. A curator must turn auto-index off first.";

    public const string FilmHasOtherEpisodes =
        "Film podcast has other (removed) episodes. A curator must clear them before Film apply.";

    public const string ServiceIdsWouldBeDropped =
        "The TV show / news organisation would not keep the podcast's YouTube / Spotify / Apple ids, so submits and " +
        "the indexer would stop matching it. Merge and deploy PR #1050 (parents keep indexing ids) before apply.";

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
        var onAllowlist = CatalogueNewsOrganisationAllowlist.Contains(podcast.Name);
        string? block = null;

        switch (kind)
        {
            case SubmitClassification.NewsReport:
                if (suggestion.ContentKind != SubmitClassification.NewsReport || suggestion.RequiresCurator)
                {
                    return null;
                }

                planEpisodes = Identify.IsYouTubeOnlyNewsOrganisation(podcast, active)
                    ? active
                    : active.Where(Identify.IsNewsReportEpisode).ToList();
                if (planEpisodes.Count == 0)
                {
                    return null;
                }

                if (!onAllowlist)
                {
                    block = NotOnAllowlist;
                }
                else if (planEpisodes.Count != active.Count)
                {
                    block = PartialNews;
                }

                break;
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

                break;
        }

        if (block is null && (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))
        {
            block = AutoIndexed;
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
            onAllowlist,
            block);
    }

    /// <summary>The TV show / news organisation the writer would store for this podcast; null for Film.</summary>
    public static Publisher? DestinationParent(Podcast podcast, string kind) => kind switch
    {
        SubmitClassification.NewsReport => CatalogueParentKindMapper.ToNewsOrganisation(podcast),
        SubmitClassification.TvShowEpisode => CatalogueParentKindMapper.ToTvShow(podcast),
        _ => null
    };
}
