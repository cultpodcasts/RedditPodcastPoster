using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Migration;

public static class CatalogueMigrateMover
{
    public static CatalogueMigrateMovePlan Plan(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        string contentKind)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKind);

        var active = episodes.Where(episode => !episode.Removed).ToList();
        return contentKind switch
        {
            SubmitClassification.NewsReport => PlanNews(podcast, active),
            SubmitClassification.TvShowEpisode => PlanTv(podcast, active),
            SubmitClassification.Film => PlanFilm(podcast, active),
            _ => CatalogueMigrateMovePlan.Rejected(
                contentKind,
                podcast.Id,
                "Kind is not a corpus-migrate target.")
        };
    }

    private static CatalogueMigrateMovePlan PlanNews(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var reports = episodes
            .Select(episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation))
            .ToList();
        return new CatalogueMigrateMovePlan(
            Accepted: true,
            ContentKind: SubmitClassification.NewsReport,
            SourcePodcastId: podcast.Id,
            DestParentId: organisation.Id,
            DestPlayableIds: reports.Select(report => report.Id).ToArray(),
            RequiresAllowlist: true,
            RejectReason: null);
    }

    private static CatalogueMigrateMovePlan PlanTv(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        var playables = episodes
            .Select(episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show))
            .ToList();
        return new CatalogueMigrateMovePlan(
            Accepted: true,
            ContentKind: SubmitClassification.TvShowEpisode,
            SourcePodcastId: podcast.Id,
            DestParentId: show.Id,
            DestPlayableIds: playables.Select(playable => playable.Id).ToArray(),
            RequiresAllowlist: false,
            RejectReason: null);
    }

    private static CatalogueMigrateMovePlan PlanFilm(Podcast podcast, IReadOnlyList<Episode> episodes)
    {
        if (episodes.Count != 1)
        {
            return CatalogueMigrateMovePlan.Rejected(
                SubmitClassification.Film,
                podcast.Id,
                "Film migrate needs exactly one active episode, because Film is a one-off with no parent.");
        }

        var film = CatalogueParentKindMapper.ToFilm(podcast, episodes[0]);
        return new CatalogueMigrateMovePlan(
            Accepted: true,
            ContentKind: SubmitClassification.Film,
            SourcePodcastId: podcast.Id,
            DestParentId: null,
            DestPlayableIds: [film.Id],
            RequiresAllowlist: false,
            RejectReason: null);
    }
}

public sealed record CatalogueMigrateMovePlan(
    bool Accepted,
    string ContentKind,
    Guid SourcePodcastId,
    Guid? DestParentId,
    Guid[] DestPlayableIds,
    bool RequiresAllowlist,
    string? RejectReason)
{
    public static CatalogueMigrateMovePlan Rejected(string contentKind, Guid sourcePodcastId, string reason) =>
        new(
            Accepted: false,
            ContentKind: contentKind,
            SourcePodcastId: sourcePodcastId,
            DestParentId: null,
            DestPlayableIds: [],
            RequiresAllowlist: contentKind == SubmitClassification.NewsReport,
            RejectReason: reason);
}
