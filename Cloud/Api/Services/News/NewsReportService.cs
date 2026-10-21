using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news report. Mirrors <c>TvShowEpisodeService</c> for lookup (by report id).
/// Service links are not editable here. Post status (posted / tweeted / unBluesky) follows the episode edit API.
/// Guests follow the episode edit API: they are not in search documents or subjects, so a guest edit is a plain save.
/// </summary>
public class NewsReportService(
    INewsReportRepository newsReportRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IPlayableSocialRetraction socialRetraction,
    ILogger<NewsReportService> logger) : INewsReportService
{
    public async Task<NewsReportGetResult> GetAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            var report = await newsReportRepository.GetBy(item => item.Id == reportId);
            return report is null
                ? new NewsReportGetResult(NewsReportGetStatus.NotFound)
                : new NewsReportGetResult(NewsReportGetStatus.Found, report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get news report '{id}'.", nameof(GetAsync), reportId);
            return new NewsReportGetResult(NewsReportGetStatus.Failed);
        }
    }

    public async Task<NewsReportUpdateResult> UpdateAsync(
        NewsReportChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await newsReportRepository.GetBy(item => item.Id == request.ReportId);
            if (report is null)
            {
                return new NewsReportUpdateResult(NewsReportUpdateStatus.NotFound);
            }

            if (!PlayableChangeApplier.TryApply(report, request.Change, out var postStatus, out var error))
            {
                return new NewsReportUpdateResult(NewsReportUpdateStatus.BadRequest, error);
            }

            // Same order as EpisodeUpdateService: Bluesky delete before the save (needs the AT URI), tweet after.
            var target = new SocialPostRemovalTarget(report.Id, report.NewsOrganisationName ?? string.Empty,
                report.Length, report.ReleaseSort, report.BlueskyPost);
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, report, target)
            };
            await newsReportRepository.Save(report);
            outcome.TweetDeleted = await socialRetraction.RemoveTweet(postStatus, target);

            // The newsreport search indexer only adds and updates (high-water mark on _ts); it never deletes.
            // As the episode API, removed=true always deletes the document, so a retry finishes the job.
            // A restored report is re-pushed by that indexer because the save bumped _ts.
            if (request.Change.Removed == true)
            {
                await searchIndexCleanup.DeleteSearchEntry(
                    report.NewsOrganisationName ?? string.Empty, report.Id, cancellationToken);
            }

            return new NewsReportUpdateResult(NewsReportUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update news report '{id}'.", nameof(UpdateAsync),
                request.ReportId);
            return new NewsReportUpdateResult(NewsReportUpdateStatus.Failed);
        }
    }
}
