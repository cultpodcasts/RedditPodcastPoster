using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Resolvers;

/// <summary>
/// Finds a playable that is not a podcast episode by its id. Ids are globally unique and the catalogue migration keeps
/// each episode's id, so a GET by episode id that misses the episode tables looks here next. The parent is read for its
/// current name and removed flag (the denormalised copy on the item is the fallback when the parent document is missing).
/// <para>
/// Kind publish (<c>POST /film|newsreport|tvshowepisode/{id}/publish</c>) supplies <see cref="ContentKind"/> and uses
/// the single-container overload — one lookup. Guid-only episode GET and guid-only publish fallback still fan out to
/// news + TV + film (cross-partition); those callers have no kind prefix. Revisit if an anonymous route ever uses the
/// three-way path.
/// </para>
/// </summary>
public class CataloguePlayableResolver(
    INewsReportRepository newsReportRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ITvShowRepository tvShowRepository,
    IFilmRepository filmRepository) : ICataloguePlayableResolver
{
    public async Task<CataloguePlayableResolution?> Resolve(Guid id, CancellationToken cancellationToken)
    {
        var reportTask = newsReportRepository.GetBy(x => x.Id == id);
        var tvEpisodeTask = tvShowEpisodeRepository.GetBy(x => x.Id == id);
        var filmTask = filmRepository.GetFilm(id);
        await Task.WhenAll(reportTask, tvEpisodeTask, filmTask);

        if (reportTask.Result is { } report)
        {
            return await ToNewsResolution(report);
        }

        if (tvEpisodeTask.Result is { } tvEpisode)
        {
            return await ToTvResolution(tvEpisode);
        }

        if (filmTask.Result is { } film)
        {
            return ToFilmResolution(film);
        }

        return null;
    }

    public async Task<CataloguePlayableResolution?> Resolve(
        Guid id,
        ContentKind expectedKind,
        CancellationToken cancellationToken)
    {
        return expectedKind switch
        {
            ContentKind.NewsReport => await ResolveNewsReport(id),
            ContentKind.TvShowEpisode => await ResolveTvShowEpisode(id),
            ContentKind.Film => await ResolveFilm(id),
            _ => null
        };
    }

    private async Task<CataloguePlayableResolution?> ResolveNewsReport(Guid id)
    {
        var report = await newsReportRepository.GetBy(x => x.Id == id);
        return report is null ? null : await ToNewsResolution(report);
    }

    private async Task<CataloguePlayableResolution?> ResolveTvShowEpisode(Guid id)
    {
        var tvEpisode = await tvShowEpisodeRepository.GetBy(x => x.Id == id);
        return tvEpisode is null ? null : await ToTvResolution(tvEpisode);
    }

    private async Task<CataloguePlayableResolution?> ResolveFilm(Guid id)
    {
        var film = await filmRepository.GetFilm(id);
        return film is null ? null : ToFilmResolution(film);
    }

    private async Task<CataloguePlayableResolution> ToNewsResolution(NewsReport report)
    {
        var organisation = await newsOrganisationRepository.GetNewsOrganisation(report.NewsOrganisationId)
                           ?? new NewsOrganisation(report.NewsOrganisationName ?? string.Empty)
                           {
                               Id = report.NewsOrganisationId,
                               Removed = report.ParentRemoved
                           };
        var name = organisation.Name.Trim();
        var parentRemoved = report.ParentRemoved == true || organisation.IsRemoved();
        return new CataloguePlayableResolution(
            nameof(ContentKind.NewsReport),
            CatalogueParentKindMapper.ToPostProjection(report, organisation),
            organisation.Id,
            name,
            name,
            report.IsRemoved() || parentRemoved,
            parentRemoved);
    }

    private async Task<CataloguePlayableResolution> ToTvResolution(TvShowEpisode tvEpisode)
    {
        var show = await tvShowRepository.GetTvShow(tvEpisode.TvShowId)
                   ?? new TvShow(tvEpisode.TvShowName ?? string.Empty)
                   {
                       Id = tvEpisode.TvShowId,
                       Removed = tvEpisode.ParentRemoved
                   };
        var name = show.Name.Trim();
        var parentRemoved = tvEpisode.ParentRemoved == true || show.IsRemoved();
        return new CataloguePlayableResolution(
            nameof(ContentKind.TvShowEpisode),
            CatalogueParentKindMapper.ToPostProjection(tvEpisode, show),
            show.Id,
            name,
            name,
            tvEpisode.IsRemoved() || parentRemoved,
            parentRemoved);
    }

    private static CataloguePlayableResolution ToFilmResolution(RedditPodcastPoster.Models.Films.Film film) =>
        new(
            nameof(ContentKind.Film),
            CatalogueParentKindMapper.ToPostProjection(film),
            null,
            null,
            film.Name.Trim(),
            film.IsRemoved(),
            ParentRemoved: false);
}
