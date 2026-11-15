using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.News;

/// <summary>
/// Moves one news report into a TV show ("Move to TV show", #1368), as a <see cref="TvShowEpisode"/> with the same id.
/// The target side is the one Move to TV show uses for a podcast episode (<see cref="TvShowMoveTarget"/>: an existing
/// show by id or name, or with <c>createShow</c> a new show with a fresh id, the publisher, no <c>indexing</c> and a
/// best-effort identity fill). The order mirrors <see cref="EpisodeTvShowMoveService"/>, so every failure stops before
/// the report row is lost:
/// <list type="number">
/// <item>Save the TV episode (<see cref="CatalogueParentKindMapper.ToTvShowEpisode(NewsReport, TvShow)"/>: services,
/// subjects, posted / tweeted / Bluesky state). A created show is saved first.</item>
/// <item>Swap the search document under the same id to ContentKind TvShowEpisode. On failure the report is kept.</item>
/// <item>Re-read the TV episode and the show; if either is gone (a concurrent delete), stop with the report kept.</item>
/// <item>Delete the report row.</item>
/// <item>Raise the show's <c>latestReleased</c> with a conditional field patch (best effort).</item>
/// <item>Delete the report's short-link record (best effort).</item>
/// </list>
/// Re-sending resumes: a TV episode with the report id in the target show while the report row exists is the
/// interrupted move, so it is not saved twice; with the report row gone, a re-send is accepted again for that show.
/// The source news organisation is never changed or deleted, even when it has no reports left; the result names it in
/// <see cref="EpisodeTvShowMoveResult.EmptiedNewsOrganisationId"/> so the curator can retire it.
/// </summary>
public class NewsReportTvShowMoveService(
    INewsReportRepository newsReportRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ICatalogueIdentityEnricher identityEnricher,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<NewsReportTvShowMoveService> logger) : INewsReportTvShowMoveService
{
    public async Task<EpisodeTvShowMoveResult> MoveAsync(
        NewsReportTvShowMoveCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (TvShowMoveTarget.Validate(request) is { } invalid)
        {
            return invalid;
        }

        try
        {
            var report = await newsReportRepository.GetBy(item => item.Id == command.ReportId);
            var existing = await tvShowEpisodeRepository.GetBy(item => item.Id == command.ReportId);
            var existingShow = existing is null ? null : await tvShowRepository.GetTvShow(existing.TvShowId);
            if (report is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the TV episode has the report id.
                if (existing is null)
                {
                    return TvShowMoveTarget.NotFound("News report not found");
                }

                if (!TvShowMoveTarget.Targets(request, existing, existingShow))
                {
                    return TvShowMoveTarget.Conflict(existing, "The news report was already moved to another TV show");
                }

                logger.LogInformation(
                    "{method}: News report '{reportId}' was already moved to TV show '{tvShowId}'; accepting the re-send.",
                    nameof(MoveAsync), command.ReportId, existing.TvShowId);
                return TvShowMoveTarget.Accepted(existing.TvShowId, existing.TvShowName, existing.Id, createdShow: false);
            }

            if (report.Removed)
            {
                return TvShowMoveTarget.BadRequest("A removed news report cannot be moved to a TV show");
            }

            if (existing is not null && !TvShowMoveTarget.Targets(request, existing, existingShow))
            {
                return TvShowMoveTarget.Conflict(existing, "The news report id is already a TV episode of another show");
            }

            if (await filmRepository.GetFilm(report.Id) is { } film)
            {
                logger.LogError(
                    "{method}: News report '{reportId}' is still in news organisation '{organisationId}' but film '{filmId}' has its id.",
                    nameof(MoveAsync), report.Id, report.NewsOrganisationId, film.Id);
                return new EpisodeTvShowMoveResult(
                    EpisodeTvShowMoveStatus.Conflict,
                    Error: "The news report id is already a film. Finish the move to film instead");
            }

            var (show, created, stop) = existing is not null
                ? TvShowMoveTarget.ResumeShow(existingShow)
                : await TvShowMoveTarget.ResolveShow(
                    request, tvShowRepository, fileKeyAllocator, identityEnricher, logger, cancellationToken);
            if (stop is not null)
            {
                return stop;
            }

            TvShowEpisode tvEpisode;
            if (existing is not null)
            {
                logger.LogWarning(
                    "{method}: TV episode '{reportId}' already exists in show '{tvShowId}' while still in news organisation '{organisationId}'; resuming the move.",
                    nameof(MoveAsync), existing.Id, existing.TvShowId, report.NewsOrganisationId);
                tvEpisode = existing;
            }
            else
            {
                // A news report already keeps Services only (url + image), as a TV episode does, so nothing is
                // materialised from platform ids.
                tvEpisode = CatalogueParentKindMapper.ToTvShowEpisode(report, show!);
                if (!string.IsNullOrWhiteSpace(request.Title))
                {
                    tvEpisode.Title = request.Title.Trim();
                }

                if (created)
                {
                    show!.LatestReleased = tvEpisode.ReleaseSort;
                    await tvShowRepository.Save(show);
                    logger.LogInformation(
                        "{method}: Created TV show '{tvShowName}' ('{tvShowId}') with no indexing for news report '{reportId}'.",
                        nameof(MoveAsync), show.Name, show.Id, report.Id);
                }

                await tvShowEpisodeRepository.Save(tvEpisode);
            }

            // Same document shape as the TV episode PATCH and the tv-show-episode pull indexer, so the next pull run
            // does not rewrite it.
            if (!await searchSwap.UploadAsync([CataloguePlayableSearchDocument.For(tvEpisode)], cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed for TV episode '{reportId}'. News report row kept; re-send to resume.",
                    nameof(MoveAsync), tvEpisode.Id);
                return TvShowMoveTarget.Failed(show!, tvEpisode, created);
            }

            // Last check before the only irreversible step: a concurrent delete of the show or the new TV episode
            // would leave the report with nowhere to live once its row is gone.
            var currentShow = await tvShowRepository.GetTvShow(show!.Id);
            var currentEpisode = await tvShowEpisodeRepository.GetTvShowEpisode(show.Id, tvEpisode.Id);
            if (currentShow is null || currentEpisode is null)
            {
                logger.LogError(
                    "{method}: TV show '{tvShowId}' or TV episode '{reportId}' is gone (changed concurrently). News report row kept; re-send to resume.",
                    nameof(MoveAsync), show.Id, tvEpisode.Id);
                return TvShowMoveTarget.Failed(show, tvEpisode, created);
            }

            await newsReportRepository.Delete(report.NewsOrganisationId, report.Id);
            logger.LogInformation(
                "Moved news report '{reportId}' from news organisation '{organisationId}' to TV show '{tvShowName}' ('{tvShowId}').",
                report.Id, report.NewsOrganisationId, currentShow.Name, currentShow.Id);
            await TvShowMoveTarget.RaiseLatestReleased(tvShowRepository, logger, currentShow, currentEpisode);
            await NewsReportMoveSource.DeleteShortLink(shortnerService, logger, report);
            var emptied = await NewsReportMoveSource.EmptiedOrganisation(
                newsReportRepository, newsOrganisationRepository, logger, report);
            return TvShowMoveTarget.Accepted(currentShow.Id, currentShow.Name, currentEpisode.Id, created) with
            {
                EmptiedNewsOrganisationId = emptied
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed to move news report '{reportId}' to a TV show.",
                nameof(MoveAsync), command.ReportId);
            return new EpisodeTvShowMoveResult(EpisodeTvShowMoveStatus.Failed);
        }
    }
}
