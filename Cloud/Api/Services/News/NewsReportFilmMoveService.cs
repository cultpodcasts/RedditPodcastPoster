using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.News;

/// <summary>
/// Moves one news report into a new Film ("Move to film", #1368), the way <see cref="EpisodeFilmMoveService"/> moves a
/// podcast episode:
/// <list type="number">
/// <item>Check the new film (<see cref="FilmMoveTarget"/>: name = request title else the report title, no '/', no
/// film with the name, and the IMDb/TMDB identity gate; an ambiguous identity returns
/// <see cref="EpisodeFilmMoveStatus.IdentityDecisionRequired"/> with candidates and writes nothing).</item>
/// <item>Map with <see cref="CatalogueParentKindMapper.ToFilm(NewsOrganisation, NewsReport)"/> (report id, services,
/// subjects, posted / tweeted / Bluesky state; publisher social fields from the news organisation), save it.</item>
/// <item>Swap the search document under the same id to ContentKind Film. On failure the report is kept.</item>
/// <item>Re-read the film; if it is gone (a concurrent delete), stop with the report kept.</item>
/// <item>Delete the report row, then its short-link record (best effort).</item>
/// </list>
/// Re-sending resumes: a film with the report id while the report row still exists is the interrupted move, so the
/// film is reused; after a completed move (report row gone, film present) a re-send is accepted again.
/// The source news organisation is never changed or deleted, even when it has no reports left; the result names it in
/// <see cref="EpisodeFilmMoveResult.EmptiedNewsOrganisationId"/> so the curator can retire it.
/// </summary>
public class NewsReportFilmMoveService(
    INewsReportRepository newsReportRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ICatalogueIdentityEnricher identityEnricher,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<NewsReportFilmMoveService> logger) : INewsReportFilmMoveService
{
    public async Task<EpisodeFilmMoveResult> MoveAsync(NewsReportFilmMoveCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var report = await newsReportRepository.GetBy(item => item.Id == command.ReportId);
            if (report is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the film has the report id.
                var moved = await filmRepository.GetFilm(command.ReportId);
                if (moved is null)
                {
                    return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound, Error: "News report not found");
                }

                logger.LogInformation(
                    "{method}: News report '{reportId}' was already moved to film '{filmId}'; accepting the re-send.",
                    nameof(MoveAsync), command.ReportId, moved.Id);
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, moved.Id, moved.Name);
            }

            if (report.Removed)
            {
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.BadRequest,
                    Error: "A removed news report cannot be moved to a film");
            }

            if (await tvShowEpisodeRepository.GetBy(item => item.Id == report.Id) is { } tvEpisode)
            {
                logger.LogError(
                    "{method}: News report '{reportId}' is still in news organisation '{organisationId}' but TV episode in show '{tvShowId}' has its id.",
                    nameof(MoveAsync), report.Id, report.NewsOrganisationId, tvEpisode.TvShowId);
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.Conflict,
                    Error: "The news report id is already a TV episode. Finish the move to TV show instead");
            }

            var film = await filmRepository.GetFilm(report.Id);
            if (film is not null)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' already exists for news report still in news organisation '{organisationId}'; resuming the move.",
                    nameof(MoveAsync), film.Id, report.NewsOrganisationId);
            }
            else
            {
                var (stop, name, admission) = await FilmMoveTarget.PrepareNewFilm(
                    report.Title, report.Release, command.Request, filmRepository, identityEnricher, logger,
                    cancellationToken);
                if (stop is not null)
                {
                    return stop;
                }

                film = CatalogueParentKindMapper.ToFilm(await Organisation(report), report);
                film.Name = name;
                film.FileKey = await fileKeyAllocator.Allocate(FileKeyKind.Film, name, film.Id);
                // ToFilm copies publisher fields from the organisation, including Removed. Here the (active) report is
                // the unit that moves, so the film is active even when it leaves a removed organisation.
                film.Removed = null;
                FilmMoveTarget.ApplyIdentity(film, admission, identityEnricher, logger);
                await filmRepository.Save(film);
            }

            // Same document shape as the film PATCH and the film pull indexer, so the next pull run does not rewrite it.
            if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(film)], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed for film '{filmId}'. News report row kept; re-send to resume.",
                    nameof(MoveAsync), film.Id);
                return Failed(film);
            }

            // Last check before the only irreversible step: a concurrent delete of the film would leave the report
            // with nowhere to live once its row is gone.
            if (await filmRepository.GetFilm(film.Id) is null)
            {
                logger.LogError(
                    "{method}: Film '{filmId}' is gone (changed concurrently). News report row kept; re-send to resume.",
                    nameof(MoveAsync), film.Id);
                return Failed(film);
            }

            await newsReportRepository.Delete(report.NewsOrganisationId, report.Id);
            logger.LogInformation(
                "Moved news report '{reportId}' from news organisation '{organisationId}' to film '{filmName}'.",
                report.Id, report.NewsOrganisationId, film.Name);
            await NewsReportMoveSource.DeleteShortLink(shortnerService, logger, report);
            var emptied = await NewsReportMoveSource.EmptiedOrganisation(newsReportRepository, logger, report);
            return new EpisodeFilmMoveResult(
                EpisodeFilmMoveStatus.Accepted, film.Id, film.Name, EmptiedNewsOrganisationId: emptied);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to move news report '{reportId}' to a film.",
                nameof(MoveAsync), command.ReportId);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed);
        }
    }

    /// <summary>
    /// The report's organisation, for the film's publisher social fields. An orphaned report (organisation deleted)
    /// still moves, with only the denormalised organisation name to stand in.
    /// </summary>
    private async Task<NewsOrganisation> Organisation(NewsReport report)
    {
        var organisation = await newsOrganisationRepository.GetNewsOrganisation(report.NewsOrganisationId);
        if (organisation is not null)
        {
            return organisation;
        }

        logger.LogWarning(
            "{method}: News organisation '{organisationId}' of report '{reportId}' was not found; the film gets no publisher fields.",
            nameof(Organisation), report.NewsOrganisationId, report.Id);
        return new NewsOrganisation(report.NewsOrganisationName ?? string.Empty) { Id = report.NewsOrganisationId };
    }

    private static EpisodeFilmMoveResult Failed(Film film) =>
        new(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
}
