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
/// Service links (<c>services</c>) use the same PATCH semantics as film. Post status (posted / tweeted / unBluesky)
/// follows the episode edit API. Guests follow the episode edit API: they are not in search documents or subjects,
/// so a guest edit is a plain save.
/// </summary>
public class NewsReportService(
    INewsReportRepository newsReportRepository,
    CataloguePlayableSearchSync searchSync,
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

            // Projected before the PATCH, so the match uses the release, length and AT URI the post was made with.
            var target = SocialPostRemovalTarget.For(report);

            if (!PlayableChangeApplier.TryApply(report, request.Change, out var postStatus, out var error))
            {
                return new NewsReportUpdateResult(NewsReportUpdateStatus.BadRequest, error);
            }

            // Both deletes run before the save, so the stored flags only drop once a delete has succeeded:
            // Bluesky needs the AT URI still stored; tweeted is cleared only when the tweet was deleted.
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, report, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, report, target)
            };
            await newsReportRepository.Save(report);

            // As the episode API: removed=true always deletes the search document and the short link (the newsreport
            // pull indexer never deletes), so a retry finishes the job; any other edit pushes the document now.
            outcome.SearchIndexer = await searchSync.AfterSave(
                report, target.Projection, request.Change.Removed == true, cancellationToken);

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
