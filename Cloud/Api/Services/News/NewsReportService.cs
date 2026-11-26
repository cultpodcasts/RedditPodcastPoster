using Api.Models;
using Api.Services;
using Api.Services.Catalogue;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news report. Mirrors <c>TvShowEpisodeService</c> for lookup (by report id).
/// Service links (<c>services</c>) use the same PATCH semantics as film. Post status (<c>blueskyPosted</c>
/// as the tweeted mirror, plus <c>unBluesky</c> delete alias) follows the episode edit API.
/// Guests follow the episode edit API: they are not in search documents or subjects,
/// so a guest edit is a plain save.
/// </summary>
public class NewsReportService(
    INewsReportRepository newsReportRepository,
    CataloguePlayableSearchSync searchSync,
    IPlayableSocialRetraction socialRetraction,
    IUrlMembershipLookup urlMembershipLookup,
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
            var hadAdditionalYouTube = report.AdditionalYouTube is { Count: > 0 };

            if (request.Change.AdditionalYouTube is not null ||
                !string.IsNullOrWhiteSpace(request.Change.PromoteYouTubeId))
            {
                if (!PlayableChangeApplier.TryValidate(report, request.Change, out var validateError))
                {
                    return new NewsReportUpdateResult(NewsReportUpdateStatus.BadRequest, validateError);
                }

                var held = await YouTubeConflict(report, request.Change, cancellationToken);
                if (held is not null)
                {
                    return new NewsReportUpdateResult(NewsReportUpdateStatus.Conflict, held);
                }

                var saved = await SaveAdditionalYouTube(report, request.Change, target, cancellationToken);
                if (saved.Result.Status != NewsReportUpdateStatus.Accepted || saved.Report is null)
                {
                    return saved.Result;
                }

                var cleared = hadAdditionalYouTube && saved.Report.AdditionalYouTube is not { Count: > 0 };
                saved.Result.Outcome!.SearchIndexer = await searchSync.AfterSave(
                    saved.Report,
                    target.Projection,
                    request.Change.Removed == true,
                    cancellationToken,
                    youTubeAlternatesCleared: cleared);
                return saved.Result;
            }

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
            var youTubeAlternatesCleared = hadAdditionalYouTube && report.AdditionalYouTube is not { Count: > 0 };
            outcome.SearchIndexer = await searchSync.AfterSave(
                report,
                target.Projection,
                request.Change.Removed == true,
                cancellationToken,
                youTubeAlternatesCleared: youTubeAlternatesCleared);

            return new NewsReportUpdateResult(NewsReportUpdateStatus.Accepted, Outcome: outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update news report '{id}'.", nameof(UpdateAsync),
                request.ReportId);
            return new NewsReportUpdateResult(NewsReportUpdateStatus.Failed);
        }
    }

    private async Task<string?> YouTubeConflict(
        NewsReport report,
        NewsReportChangeRequest change,
        CancellationToken cancellationToken) =>
        await YouTubeAlternateOwnership.ConflictMessageIfHeld(
            urlMembershipLookup,
            YouTubeAlternateChangeApplier.NewIds(
                report,
                change.AdditionalYouTube,
                YouTubeAlternateChangeApplier.ResultingPrimaryYouTubeId(report, change.Services)),
            [report.Id],
            cancellationToken);

    private readonly record struct NewsReportPersist(NewsReportUpdateResult Result, NewsReport? Report);

    /// <summary>
    /// Re-reads the report and applies this request onto that instance, then replaces it with that instance's ETag.
    /// A keep-list is a 409, before the apply, when that read has an additional YouTube id the client did not list.
    /// </summary>
    private async Task<NewsReportPersist> SaveAdditionalYouTube(
        NewsReport firstRead,
        NewsReportChangeRequest change,
        SocialPostRemovalTarget target,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= FilmDocumentUpdate.MaxAttempts; attempt++)
        {
            var latest = await newsReportRepository.GetNewsReportWithETag(
                firstRead.NewsOrganisationId, firstRead.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: News report '{reportId}' was deleted during the edit; not saved.",
                    nameof(SaveAdditionalYouTube), firstRead.Id);
                return new NewsReportPersist(new NewsReportUpdateResult(NewsReportUpdateStatus.NotFound), null);
            }

            if (change.AdditionalYouTube is not null)
            {
                var unexpected = YouTubeAlternateChangeApplier.IdsTheClientDidNotList(
                    latest.Item.AdditionalYouTube, change.ExpectedAdditionalYouTubeIds);
                if (unexpected.Length > 0)
                {
                    logger.LogWarning(
                        "{method}: News report '{reportId}' has {count} additional YouTube id(s) this edit did not list; not saved.",
                        nameof(SaveAdditionalYouTube), firstRead.Id, unexpected.Length);
                    return new NewsReportPersist(
                        new NewsReportUpdateResult(
                            NewsReportUpdateStatus.Conflict,
                            YouTubeAlternateChangeApplier.StaleMessage("news report"),
                            UnexpectedAdditionalYouTubeIds: unexpected),
                        null);
                }
            }

            var held = await YouTubeConflict(latest.Item, change, cancellationToken);
            if (held is not null)
            {
                return new NewsReportPersist(new NewsReportUpdateResult(NewsReportUpdateStatus.Conflict, held), null);
            }

            if (!PlayableChangeApplier.TryApply(latest.Item, change, out var postStatus, out var error))
            {
                return new NewsReportPersist(new NewsReportUpdateResult(NewsReportUpdateStatus.BadRequest, error), null);
            }

            if (!await newsReportRepository.TryReplace(latest.Item, latest.ETag))
            {
                continue;
            }

            // Deletes run only after the replace. A later 409 leaves the live posts in place.
            var outcome = new EpisodeUpdateOutcome
            {
                BlueskyPostDeleted = await socialRetraction.RemoveBlueskyPost(postStatus, latest.Item, target),
                TweetDeleted = await socialRetraction.RemoveTweet(postStatus, latest.Item, target)
            };
            var report = latest.Item;
            if (outcome.TweetDeleted == true || outcome.BlueskyPostDeleted == true)
            {
                report = await PersistClearedSocialFlags(report, outcome) ?? report;
            }

            return new NewsReportPersist(
                new NewsReportUpdateResult(NewsReportUpdateStatus.Accepted, Outcome: outcome),
                report);
        }

        throw new InvalidOperationException(
            $"News report '{firstRead.Id}' kept changing during the edit; not saved.");
    }

    /// <summary>
    /// Writes cleared tweet and Bluesky flags onto a fresh read, so a field changed during the delete stays.
    /// </summary>
    private async Task<NewsReport?> PersistClearedSocialFlags(NewsReport saved, EpisodeUpdateOutcome outcome)
    {
        for (var attempt = 1; attempt <= FilmDocumentUpdate.MaxAttempts; attempt++)
        {
            var latest = await newsReportRepository.GetNewsReportWithETag(saved.NewsOrganisationId, saved.Id);
            if (latest is null)
            {
                logger.LogWarning(
                    "{method}: News report '{reportId}' was deleted while clearing social flags.",
                    nameof(PersistClearedSocialFlags), saved.Id);
                return null;
            }

            if (outcome.TweetDeleted == true)
            {
                latest.Item.ClearTweetState();
            }

            if (outcome.BlueskyPostDeleted == true)
            {
                latest.Item.ClearBlueskyPostState();
            }

            if (await newsReportRepository.TryReplace(latest.Item, latest.ETag))
            {
                return latest.Item;
            }
        }

        throw new InvalidOperationException(
            $"News report '{saved.Id}' kept changing while clearing social flags; not saved.");
    }

    public async Task<EpisodeDeleteResult> DeleteAsync(Guid reportId, CancellationToken cancellationToken)
    {
        try
        {
            var report = await newsReportRepository.GetBy(item => item.Id == reportId);
            if (report is null)
            {
                logger.LogWarning("Delete: missing news report '{reportId}'.", reportId);
                return new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound);
            }

            if (report.Tweeted)
            {
                return new EpisodeDeleteResult(
                    EpisodeDeleteStatus.AlreadySocial,
                    Tweeted: true);
            }

            var shortLinkTarget = SocialPostRemovalTarget.For(report).Projection;
            await newsReportRepository.Delete(report.NewsOrganisationId, report.Id);
            await searchSync.AfterHardDelete(
                report.Id,
                report.NewsOrganisationName ?? string.Empty,
                shortLinkTarget,
                cancellationToken);

            logger.LogWarning(
                "Delete removed news report '{reportId}' from organisation '{organisationId}'.",
                report.Id, report.NewsOrganisationId);
            return new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete news report '{reportId}'.", reportId);
            return new EpisodeDeleteResult(EpisodeDeleteStatus.Failed);
        }
    }
}
