using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Resolvers;

/// <summary>
/// Finds a playable that is not a podcast episode by its id. Ids are globally unique and the catalogue migration keeps
/// each episode's id, so a GET by episode id that misses the episode tables looks here next. The three lookups run
/// together; the parent is read for its current name and removed flag (the denormalised copy on the item is the
/// fallback when the parent document is missing).
/// Cost: the three lookups are cross-partition queries and run only when no podcast episode has the id. Both callers
/// are signed-in or curate routes taking a raw GUID (no kind prefix to pick one container), so a miss is rare and
/// bounded; revisit if an anonymous route ever uses this.
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
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(report.NewsOrganisationId)
                               ?? new NewsOrganisation(report.NewsOrganisationName ?? string.Empty)
                               {
                                   Id = report.NewsOrganisationId,
                                   Removed = report.ParentRemoved
                               };
            var name = organisation.Name.Trim();
            return new CataloguePlayableResolution(
                nameof(ContentKind.NewsReport),
                CatalogueParentKindMapper.ToPostProjection(report, organisation),
                organisation.Id,
                name,
                name,
                report.IsRemoved() || report.ParentRemoved == true || organisation.IsRemoved());
        }

        if (tvEpisodeTask.Result is { } tvEpisode)
        {
            var show = await tvShowRepository.GetTvShow(tvEpisode.TvShowId)
                       ?? new TvShow(tvEpisode.TvShowName ?? string.Empty)
                       {
                           Id = tvEpisode.TvShowId,
                           Removed = tvEpisode.ParentRemoved
                       };
            var name = show.Name.Trim();
            return new CataloguePlayableResolution(
                nameof(ContentKind.TvShowEpisode),
                CatalogueParentKindMapper.ToPostProjection(tvEpisode, show),
                show.Id,
                name,
                name,
                tvEpisode.IsRemoved() || tvEpisode.ParentRemoved == true || show.IsRemoved());
        }

        if (filmTask.Result is { } film)
        {
            return new CataloguePlayableResolution(
                nameof(ContentKind.Film),
                CatalogueParentKindMapper.ToPostProjection(film),
                null,
                null,
                film.Name.Trim(),
                film.IsRemoved());
        }

        return null;
    }
}
