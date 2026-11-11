using Api.Services.Catalogue;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.News;

/// <summary>
/// The best-effort steps after a news report row is deleted by a move to a TV show or a film (#1368). The move has
/// already succeeded by then, so a failure here is logged and never fails the request.
/// </summary>
public static class NewsReportMoveSource
{
    /// <summary>
    /// The short-link key is the report id (base 64), which the TV episode or film keeps. As in the episode moves, the
    /// record is deleted: with no record the Shortner looks the id up in search (now ContentKind TvShowEpisode or Film)
    /// and redirects to the new page in one hop.
    /// </summary>
    public static async Task DeleteShortLink(IShortnerService shortnerService, ILogger logger, NewsReport report)
    {
        try
        {
            var result = await shortnerService.Delete(SocialPostRemovalTarget.For(report).Projection);
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short-link record for '{reportId}' was not deleted (absent, or the KV delete failed).",
                    nameof(DeleteShortLink), report.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link delete failed for moved news report '{reportId}'.",
                nameof(DeleteShortLink), report.Id);
        }
    }

    /// <summary>
    /// The source news organisation's id when the move left it with no reports, so the client can offer to retire it
    /// (PATCH <c>newsorganisation/{id}</c> with <c>removed: true</c>, curate). The organisation itself is never changed
    /// or deleted by the move. Null when reports remain, when the organisation is already removed or no longer exists
    /// (an orphaned report), or when the count or the organisation read failed: there is nothing to retire then, and a
    /// retire PATCH for a missing organisation would 404 after a move that succeeded.
    /// </summary>
    public static async Task<Guid?> EmptiedOrganisation(
        INewsReportRepository newsReportRepository,
        INewsOrganisationRepository newsOrganisationRepository,
        ILogger logger,
        NewsReport report)
    {
        try
        {
            var remaining = await newsReportRepository.Count(report.NewsOrganisationId);
            if (remaining > 0)
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Could not count the reports left in news organisation '{organisationId}'.",
                nameof(EmptiedOrganisation), report.NewsOrganisationId);
            return null;
        }

        try
        {
            var organisation = await newsOrganisationRepository.GetNewsOrganisation(report.NewsOrganisationId);
            if (organisation is null)
            {
                logger.LogInformation(
                    "{method}: News organisation '{organisationId}' of moved report '{reportId}' no longer exists; nothing to retire.",
                    nameof(EmptiedOrganisation), report.NewsOrganisationId, report.Id);
                return null;
            }

            if (organisation.Removed == true)
            {
                logger.LogInformation(
                    "{method}: News organisation '{organisationId}' of moved report '{reportId}' is already removed; nothing to retire.",
                    nameof(EmptiedOrganisation), report.NewsOrganisationId, report.Id);
                return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{method}: Could not read news organisation '{organisationId}'.",
                nameof(EmptiedOrganisation), report.NewsOrganisationId);
            return null;
        }

        logger.LogInformation(
            "{method}: News organisation '{organisationId}' has no reports left after moving '{reportId}'.",
            nameof(EmptiedOrganisation), report.NewsOrganisationId, report.Id);
        return report.NewsOrganisationId;
    }
}
