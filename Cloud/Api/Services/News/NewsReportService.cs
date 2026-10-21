using System.Globalization;
using Api.Models;
using Api.Services.Episodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.News;

/// <summary>
/// Curator GET/PATCH for a news report. Mirrors <c>TvShowEpisodeService</c> for lookup (by report id).
/// Service links and social-post retraction are not editable here yet.
/// </summary>
public class NewsReportService(
    INewsReportRepository newsReportRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
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

            var wasRemoved = report.Removed;
            if (!TryApply(report, request.Change, out var error))
            {
                return new NewsReportUpdateResult(NewsReportUpdateStatus.BadRequest, error);
            }

            await newsReportRepository.Save(report);

            // The newsreport search indexer only adds and updates (high-water mark on _ts); it never deletes.
            // A restored report is re-pushed by that indexer because the save bumped _ts.
            if (report.Removed && !wasRemoved)
            {
                await searchIndexCleanup.DeleteSearchEntry(
                    report.NewsOrganisationName ?? string.Empty, report.Id, cancellationToken);
            }

            return new NewsReportUpdateResult(NewsReportUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update news report '{id}'.", nameof(UpdateAsync),
                request.ReportId);
            return new NewsReportUpdateResult(NewsReportUpdateStatus.Failed);
        }
    }

    /// <summary>Validates first, then applies, so a 400 leaves the report untouched.</summary>
    internal static bool TryApply(NewsReport report, NewsReportChangeRequest change, out string? error)
    {
        error = null;
        DateOnly? release = null;
        if (change.Release != null)
        {
            if (!DateOnly.TryParseExact(change.Release, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
            {
                error = $"Invalid release '{change.Release}': expected a calendar date yyyy-MM-dd.";
                return false;
            }

            release = parsed;
        }

        TimeSpan? length = null;
        if (!string.IsNullOrWhiteSpace(change.Duration))
        {
            if (!TimeSpan.TryParse(change.Duration, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < TimeSpan.Zero)
            {
                error = $"Invalid duration '{change.Duration}': expected h:mm:ss.";
                return false;
            }

            length = parsed;
        }

        if (!string.IsNullOrWhiteSpace(change.Title))
        {
            report.Title = change.Title.Trim();
        }

        if (change.Description != null)
        {
            report.Description = change.Description;
        }

        if (release is not null)
        {
            report.SetRelease(CatalogueRelease.FromDate(release.Value));
        }

        if (length is not null)
        {
            report.Length = length.Value;
        }

        if (change.Explicit != null)
        {
            report.Explicit = change.Explicit.Value;
        }

        if (change.Ignored != null)
        {
            report.Ignored = change.Ignored.Value;
        }

        if (change.Removed != null)
        {
            report.Removed = change.Removed.Value;
        }

        if (change.Subjects != null)
        {
            report.ApplyUserSubjects(change.Subjects);
        }

        if (change.SearchTerms != null)
        {
            report.SearchTerms = string.IsNullOrWhiteSpace(change.SearchTerms) ? null : change.SearchTerms;
        }

        if (change.HashTag != null)
        {
            report.HashTag = string.IsNullOrWhiteSpace(change.HashTag) ? null : change.HashTag.Trim();
        }

        if (change.Language != null)
        {
            report.Language = EpisodeChangeApplier.NormaliseEpisodeLanguage(change.Language);
        }

        return true;
    }
}
