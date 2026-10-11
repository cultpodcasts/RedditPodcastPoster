using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Discovery;

public class DiscoverySubmissionReviewService(
    IDiscoverySubmissionsRepository discoverySubmissionsRepository,
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryCurationSubmitService discoveryCurationSubmitService,
    TimeProvider timeProvider,
    ILogger<DiscoverySubmissionReviewService> logger) : IDiscoverySubmissionReviewService
{
    public const string ReviewSavedAction = "ReviewSaved";
    public const string ApplyStartedAction = "ApplyStarted";
    public const string AppliedAction = "Applied";
    public const string ApplyFailedAction = "ApplyFailed";
    public const string ApplyConflictAction = "ApplyConflict";
    public const string PartiallyAppliedAction = "PartiallyApplied";
    public const string RetryStartedAction = "RetryStarted";
    public const string ItemsAbandonedAction = "ItemsAbandoned";
    public const string ReleaseStartedAction = "ReleaseStarted";
    public const string ReleasedAction = "Released";
    public const string RecoveredAction = "Recovered";

    /// <summary>
    /// The Api host's <c>functionTimeout</c> (Cloud/Api/host.json). The host kills any invocation after this,
    /// so an Applying claim older than <see cref="StaleInProgressAfter"/> cannot still be running.
    /// </summary>
    public static readonly TimeSpan FunctionTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// An Applying claim older than this may be recovered by an admin. Must stay well above
    /// <see cref="FunctionTimeout"/> (asserted by unit test against host.json).
    /// </summary>
    public static readonly TimeSpan StaleInProgressAfter = TimeSpan.FromMinutes(30);

    public const string InterruptedError = "Interrupted: the operation stopped without recording this result; verify the catalogue before retrying.";
    public const int MaxCommentLength = 4000;

    public async Task<DiscoverySubmissionQueryResult<DiscoverySubmissionList>> List(SupervisionCaller caller,
        CancellationToken c)
    {
        try
        {
            if (caller.CanSuperviseAny)
            {
                var queue = (await discoverySubmissionsRepository.GetByStatus(DiscoverySubmissionStatus.Pending, c)
                        .ToListAsync(c))
                    .Concat(await discoverySubmissionsRepository.GetByStatus(DiscoverySubmissionStatus.ApplyFailed, c)
                        .ToListAsync(c))
                    .Concat(await discoverySubmissionsRepository
                        .GetByStatus(DiscoverySubmissionStatus.PartiallyApplied, c).ToListAsync(c))
                    .Concat(await discoverySubmissionsRepository
                        .GetByStatus(DiscoverySubmissionStatus.Applying, c).ToListAsync(c))
                    .Where(x => DiscoverySubmissionRules.CanReview(caller, x))
                    .OrderBy(x => x.SubmittedAt)
                    .ToArray();
                return new(DiscoverySubmissionQueryStatus.Ok,
                    new DiscoverySubmissionList(queue, queue.Length, true));
            }

            var own = (await discoverySubmissionsRepository.GetBySubmitter(caller.Subject, c).ToListAsync(c))
                .OrderByDescending(x => x.SubmittedAt)
                .ToArray();
            return new(DiscoverySubmissionQueryStatus.Ok,
                new DiscoverySubmissionList(own, own.Count(x => x.Status == DiscoverySubmissionStatus.Pending), false));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure listing discovery submissions.");
            return new(DiscoverySubmissionQueryStatus.Failed);
        }
    }

    public async Task<DiscoverySubmissionQueryResult<DiscoverySubmissionDetail>> Get(Guid submissionId,
        SupervisionCaller caller, CancellationToken c)
    {
        try
        {
            var submission = await discoverySubmissionsRepository.GetById(submissionId, c);
            if (submission == null)
            {
                return new(DiscoverySubmissionQueryStatus.NotFound);
            }

            if (!DiscoverySubmissionRules.CanView(caller, submission))
            {
                return new(DiscoverySubmissionQueryStatus.Forbidden);
            }

            var reports = await discoveryResultsService.GetDocuments(submission.DiscoveryResultsDocumentIds);
            var podcasts = await discoveryResultsService.GetPodcastInfo(reports.SelectMany(x => x.DiscoveryResults), c);
            return new(DiscoverySubmissionQueryStatus.Ok,
                new DiscoverySubmissionDetail(submission, reports, podcasts,
                    DiscoverySubmissionRules.CanReview(caller, submission) &&
                    DiscoverySubmissionRules.IsReviewable(submission)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure getting discovery submission '{SubmissionId}'.", submissionId);
            return new(DiscoverySubmissionQueryStatus.Failed);
        }
    }

    public async Task<DiscoverySubmissionCommandResult> SaveReview(Guid submissionId,
        DiscoverySubmissionReviewRequest request, SupervisionCaller caller, CancellationToken c)
    {
        var (submission, refusal) = await LoadReviewable(submissionId, request.ETag, caller, c);
        if (refusal != null)
        {
            return refusal;
        }

        var reports = await discoveryResultsService.GetDocuments(submission!.DiscoveryResultsDocumentIds);
        var knownResultIds = reports.SelectMany(x => x.DiscoveryResults).Select(x => x.Id).ToHashSet();

        var invalid = (request.AcceptedResultIds ?? [])
            .Concat(request.ItemComments?.Select(x => x.ResultId) ?? [])
            .Where(id => !knownResultIds.Contains(id))
            .Distinct()
            .ToArray();
        if (invalid.Length > 0)
        {
            return new(DiscoverySubmissionCommandStatus.Invalid,
                "Result ids are not in this submission's reports.", InvalidIds: invalid);
        }

        if (TooLong(request.ReportComment) || (request.ItemComments?.Any(x => TooLong(x.Comment)) ?? false))
        {
            return new(DiscoverySubmissionCommandStatus.Invalid, $"Comments are limited to {MaxCommentLength} characters.");
        }

        if (request.ReportComment != null)
        {
            submission.ReportComment = string.IsNullOrWhiteSpace(request.ReportComment) ? null : request.ReportComment.Trim();
        }

        if (request.ItemComments != null)
        {
            var comments = request.ItemComments
                .Where(x => !string.IsNullOrWhiteSpace(x.Comment))
                .GroupBy(x => x.ResultId)
                .Select(g => new DiscoverySubmissionItemComment { ResultId = g.Key, Comment = g.Last().Comment.Trim() })
                .ToArray();
            submission.ItemComments = comments.Length > 0 ? comments : null;
        }

        if (request.AcceptedResultIds != null)
        {
            SetFinalSet(submission, request.AcceptedResultIds);
        }

        submission.SupervisorSubject = caller.Subject;
        AppendHistory(submission, caller.Subject, ReviewSavedAction);

        var etag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (etag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        return new(DiscoverySubmissionCommandStatus.Ok, ETag: etag, SubmissionStatus: submission.Status,
            Diff: CurrentDiff(submission));
    }

    public async Task<DiscoverySubmissionCommandResult> Apply(Guid submissionId,
        DiscoverySubmissionApplyRequest request, SupervisionCaller caller, CancellationToken c)
    {
        var existing = await discoverySubmissionsRepository.GetById(submissionId, c);
        if (existing is { Status: DiscoverySubmissionStatus.Applied } &&
            DiscoverySubmissionRules.CanReview(caller, existing))
        {
            // Idempotent: a repeated apply is a no-op that reports the earlier outcome.
            return new(DiscoverySubmissionCommandStatus.Ok, "Already applied.", existing.ETag,
                existing.Status, CurrentDiff(existing));
        }

        var (submission, refusal) = await LoadReviewable(submissionId, request.ETag, caller, c, existing);
        if (refusal != null)
        {
            return refusal;
        }

        var reports = await discoveryResultsService.GetDocuments(submission!.DiscoveryResultsDocumentIds);
        var notHeld = submission.DiscoveryResultsDocumentIds
            .Where(id => reports.All(r => r.Id != id || r.State != DiscoveryResultsDocumentState.PendingSupervisor))
            .ToArray();
        if (notHeld.Length > 0)
        {
            logger.LogWarning("Apply of submission '{SubmissionId}' refused; reports no longer held: {DocumentIds}.",
                submissionId, string.Join(",", notHeld));
            return new(DiscoverySubmissionCommandStatus.Conflict,
                "One or more reports are no longer held for this submission.", InvalidIds: notHeld);
        }

        var knownResultIds = reports.SelectMany(x => x.DiscoveryResults).Select(x => x.Id).ToHashSet();
        var finalSet = (request.AcceptedResultIds ?? submission.SupervisorAcceptedResultIds ??
                        submission.SubmitterAcceptedResultIds).Distinct().ToArray();
        var invalid = finalSet.Where(id => !knownResultIds.Contains(id)).ToArray();
        if (invalid.Length > 0)
        {
            return new(DiscoverySubmissionCommandStatus.Invalid,
                "Result ids are not in this submission's reports.", InvalidIds: invalid);
        }

        // Claim with the caller's ETag AND persist Applying, so nothing else (apply, review, release, retry)
        // can act on the submission while ingestion runs.
        SetFinalSet(submission, finalSet);
        submission.SupervisorSubject = caller.Subject;
        submission.ApplyError = null;
        var previousStatus = submission.Status;
        BeginInProgress(submission, DiscoverySubmissionOperation.Apply, caller.Subject, finalSet);
        AppendHistory(submission, caller.Subject, ApplyStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var historyAtClaim = submission.History.Length;

        var result = await discoveryCurationSubmitService.ApplyHeldAsync(new DiscoverySubmitRequest
        {
            DiscoveryResultsDocumentIds = submission.DiscoveryResultsDocumentIds,
            ResultIds = finalSet
        }, c);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (result.Status == DiscoveryCurationSubmitStatus.Conflict)
        {
            // The guarded PendingSupervisor -> Processed claim failed before any ingest; nothing happened.
            submission.Status = previousStatus;
            EndInProgress(submission);
            AppendHistory(submission, caller.Subject, ApplyConflictAction);
            await Persist(submission, claimedETag, historyAtClaim, DiscoverySubmissionOperation.Apply, c);
            return new(DiscoverySubmissionCommandStatus.Conflict,
                "One or more reports are no longer held for this submission.",
                InvalidIds: result.UnavailableDocumentIds);
        }

        if (result.Outcome != null)
        {
            // Ingestion was attempted: from here the submission (not report state) tracks what is left to do.
            MergeOutcomes(submission, finalSet, result.Outcome, now,
                aborted: result.Status != DiscoveryCurationSubmitStatus.Ok);
            submission.AppliedAt = now;
            submission.Status = DiscoverySubmissionRules.DeriveStatus(submission.ItemOutcomes!);
            submission.ApplyError = submission.Status == DiscoverySubmissionStatus.Applied
                ? null
                : "One or more results failed to ingest; retry or abandon them.";
            AppendHistory(submission, caller.Subject, HistoryActionFor(submission.Status));
        }
        else
        {
            submission.Status = DiscoverySubmissionStatus.ApplyFailed;
            submission.ApplyError =
                "Apply failed before any result was submitted; reports were returned to PendingSupervisor.";
            AppendHistory(submission, caller.Subject, ApplyFailedAction);
        }

        EndInProgress(submission);
        return await Finish(submission, claimedETag, historyAtClaim, DiscoverySubmissionOperation.Apply, caller,
            result.Outcome, c);
    }

    public async Task<DiscoverySubmissionCommandResult> Retry(Guid submissionId,
        DiscoverySubmissionRetryRequest request, SupervisionCaller caller, CancellationToken c)
    {
        var submission = await discoverySubmissionsRepository.GetById(submissionId, c);
        if (submission == null)
        {
            return new(DiscoverySubmissionCommandStatus.NotFound);
        }

        if (!DiscoverySubmissionRules.CanReview(caller, submission))
        {
            return new(DiscoverySubmissionCommandStatus.Forbidden);
        }

        if (!DiscoverySubmissionRules.IsRetryable(submission))
        {
            return new(DiscoverySubmissionCommandStatus.Conflict,
                $"Submission is {submission.Status} with no failed items to retry.");
        }

        if (string.IsNullOrWhiteSpace(request.ETag) || request.ETag != submission.ETag)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var failed = submission.ItemOutcomes!
            .Where(x => x.State == DiscoverySubmissionItemState.Failed)
            .Select(x => x.ResultId)
            .ToHashSet();
        var abandon = (request.AbandonResultIds ?? []).Distinct().ToArray();
        var notFailed = abandon.Where(id => !failed.Contains(id)).ToArray();
        if (notFailed.Length > 0)
        {
            return new(DiscoverySubmissionCommandStatus.Invalid, "Only failed items can be abandoned.",
                InvalidIds: notFailed);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in submission.ItemOutcomes!.Where(x => abandon.Contains(x.ResultId)))
        {
            item.State = DiscoverySubmissionItemState.Abandoned;
        }

        if (abandon.Length > 0)
        {
            AppendHistory(submission, caller.Subject, ItemsAbandonedAction);
        }

        // Only items still Failed are resubmitted; Succeeded and Abandoned items are never re-ingested.
        var toRetry = failed.Except(abandon).ToArray();
        submission.SupervisorSubject = caller.Subject;
        if (toRetry.Length == 0)
        {
            CloseOut(submission, caller.Subject);
            return await Finish(submission, request.ETag, submission.History.Length, null, caller, null, c);
        }

        // Persist Applying (operation Retry) in the claim so a concurrent retry fails IsRetryable (409).
        BeginInProgress(submission, DiscoverySubmissionOperation.Retry, caller.Subject, toRetry);
        AppendHistory(submission, caller.Subject, RetryStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var historyAtClaim = submission.History.Length;

        var result = await discoveryCurationSubmitService.RetryItemsAsync(new DiscoverySubmitRequest
        {
            DiscoveryResultsDocumentIds = submission.DiscoveryResultsDocumentIds,
            ResultIds = toRetry
        }, c);

        if (result.Outcome != null)
        {
            MergeOutcomes(submission, toRetry, result.Outcome, now,
                aborted: result.Status != DiscoveryCurationSubmitStatus.Ok);
        }
        else
        {
            foreach (var item in submission.ItemOutcomes!.Where(x => toRetry.Contains(x.ResultId)))
            {
                item.Error = result.Status == DiscoveryCurationSubmitStatus.Conflict
                    ? "Retry refused: a report is not Processed."
                    : "Retry failed before this result was submitted.";
                item.LastAttemptAt = now;
            }
        }

        EndInProgress(submission);
        CloseOut(submission, caller.Subject);
        return await Finish(submission, claimedETag, historyAtClaim, DiscoverySubmissionOperation.Retry, caller,
            result.Outcome, c);
    }

    public async Task<DiscoverySubmissionCommandResult> Release(Guid submissionId,
        DiscoverySubmissionReleaseRequest request, SupervisionCaller caller, CancellationToken c)
    {
        if (TooLong(request.Reason))
        {
            return new(DiscoverySubmissionCommandStatus.Invalid, $"Reason is limited to {MaxCommentLength} characters.");
        }

        // Only a submission with nothing ingested (reports still held) can be released.
        var (submission, refusal) = await LoadReviewable(submissionId, request.ETag, caller, c);
        if (refusal != null)
        {
            return refusal;
        }

        BeginInProgress(submission!, DiscoverySubmissionOperation.Release, caller.Subject, null);
        AppendHistory(submission!, caller.Subject, ReleaseStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission!, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var historyAtClaim = submission!.History.Length;

        var notReleased = new List<Guid>();
        foreach (var documentId in submission!.DiscoveryResultsDocumentIds)
        {
            if (!await discoveryResultsRepository.TryTransitionState(documentId,
                    DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed, c))
            {
                notReleased.Add(documentId);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        submission.Status = DiscoverySubmissionStatus.Released;
        submission.ReleasedAt = now;
        submission.ReleaseReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        submission.SupervisorSubject = caller.Subject;
        EndInProgress(submission);
        AppendHistory(submission, caller.Subject, ReleasedAction);
        var finalETag = await Persist(submission, claimedETag, historyAtClaim, DiscoverySubmissionOperation.Release, c);
        await discoveryResultsService.UpdateDiscoveryInfoContent();
        if (finalETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.ApplyFailed, OutcomeNotRecorded, null,
                DiscoverySubmissionStatus.Applying);
        }

        if (notReleased.Count > 0)
        {
            logger.LogWarning("Released submission '{SubmissionId}' but reports were not held: {DocumentIds}.",
                submissionId, string.Join(",", notReleased));
        }

        return new(DiscoverySubmissionCommandStatus.Ok,
            notReleased.Count > 0 ? "Some reports were not held and were left unchanged." : null,
            finalETag, submission.Status, InvalidIds: notReleased.Count > 0 ? notReleased : null);
    }

    public async Task<DiscoverySubmissionCommandResult> ReleaseStuckReport(Guid documentId,
        DiscoveryReportReleaseRequest request, SupervisionCaller caller, CancellationToken c)
    {
        if (!caller.IsAdmin)
        {
            return new(DiscoverySubmissionCommandStatus.Forbidden);
        }

        var report = await discoveryResultsRepository.GetById(documentId);
        if (report == null)
        {
            return new(DiscoverySubmissionCommandStatus.NotFound);
        }

        if (report.State != DiscoveryResultsDocumentState.PendingSupervisor)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, $"Report is {report.State}, not PendingSupervisor.");
        }

        var live = (await discoverySubmissionsRepository.GetByStatus(DiscoverySubmissionStatus.Pending, c).ToListAsync(c))
            .Concat(await discoverySubmissionsRepository.GetByStatus(DiscoverySubmissionStatus.ApplyFailed, c)
                .ToListAsync(c))
            .Concat(await discoverySubmissionsRepository.GetByStatus(DiscoverySubmissionStatus.Applying, c)
                .ToListAsync(c))
            .Where(x => (DiscoverySubmissionRules.IsReviewable(x) || x.Status == DiscoverySubmissionStatus.Applying) &&
                        x.DiscoveryResultsDocumentIds.Contains(documentId))
            .Select(x => x.Id)
            .ToArray();
        if (live.Length > 0)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict,
                "Report is held by a live submission; release that submission instead.", InvalidIds: live);
        }

        if (!await discoveryResultsRepository.TryTransitionState(documentId,
                DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed, c))
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Report changed concurrently.");
        }

        logger.LogWarning("Admin '{Admin}' released stuck report '{DocumentId}' to Unprocessed. Reason: {Reason}.",
            caller.Subject, documentId, request.Reason);
        await discoveryResultsService.UpdateDiscoveryInfoContent();
        return new(DiscoverySubmissionCommandStatus.Ok, "Report released to Unprocessed.");
    }

    public async Task<DiscoverySubmissionCommandResult> Recover(Guid submissionId,
        DiscoverySubmissionRecoverRequest request, SupervisionCaller caller, CancellationToken c)
    {
        if (!caller.IsAdmin)
        {
            return new(DiscoverySubmissionCommandStatus.Forbidden);
        }

        var submission = await discoverySubmissionsRepository.GetById(submissionId, c);
        if (submission == null)
        {
            return new(DiscoverySubmissionCommandStatus.NotFound);
        }

        if (submission.Status != DiscoverySubmissionStatus.Applying)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, $"Submission is {submission.Status}, not Applying.");
        }

        if (string.IsNullOrWhiteSpace(request.ETag) || request.ETag != submission.ETag)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (submission.InProgressSince is { } since && now - since < StaleInProgressAfter)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict,
                $"Operation started {since:O}; it can be recovered after {StaleInProgressAfter.TotalMinutes} minutes.");
        }

        var reports = await discoveryResultsService.GetDocuments(submission.DiscoveryResultsDocumentIds);
        switch (submission.InProgressOperation)
        {
            case DiscoverySubmissionOperation.Release:
                // Complete the release: any report still held goes back to Unprocessed (guarded).
                foreach (var report in reports.Where(r => r.State == DiscoveryResultsDocumentState.PendingSupervisor))
                {
                    await discoveryResultsRepository.TryTransitionState(report.Id,
                        DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed, c);
                }

                submission.Status = DiscoverySubmissionStatus.Released;
                submission.ReleasedAt ??= now;
                break;
            case DiscoverySubmissionOperation.Apply
                when reports.Any(r => r.State == DiscoveryResultsDocumentState.PendingSupervisor):
                // Apply claims every report (PendingSupervisor -> Processed) before ingesting anything, so a report
                // still held means the claim loop never finished and nothing was ingested. Undo any partial claim
                // (guarded Processed -> PendingSupervisor) and return the submission to where it was, so it can be
                // applied again or released.
                foreach (var report in reports.Where(r => r.State == DiscoveryResultsDocumentState.Processed))
                {
                    if (!await discoveryResultsRepository.TryTransitionState(report.Id,
                            DiscoveryResultsDocumentState.Processed, DiscoveryResultsDocumentState.PendingSupervisor, c))
                    {
                        logger.LogWarning(
                            "Recover of submission '{SubmissionId}': report '{ReportId}' changed; could not return it to PendingSupervisor.",
                            submissionId, report.Id);
                    }
                }

                submission.Status = submission.StatusBeforeInProgress ?? DiscoverySubmissionStatus.Pending;
                break;
            default:
                // Every report was claimed, so ingestion may have happened: results without a recorded outcome become
                // Failed (interrupted). Retrying one that was in fact ingested before the crash does not duplicate it:
                // DiscoveryUrlSubmitter matches the existing episode (PodcastProcessor.AddEpisodeToExistingPodcast
                // enriches it rather than creating; catalogue kinds report EpisodeAlreadyExists, see CatalogueKindSubmitRules).
                var byId = (submission.ItemOutcomes ?? []).ToDictionary(x => x.ResultId);
                foreach (var id in submission.InProgressResultIds ?? [])
                {
                    if (!byId.TryGetValue(id, out var item))
                    {
                        item = new DiscoverySubmissionItemOutcome { ResultId = id };
                        byId[id] = item;
                    }

                    if (item.State != DiscoverySubmissionItemState.Succeeded)
                    {
                        item.State = DiscoverySubmissionItemState.Failed;
                        item.Error = InterruptedError;
                    }
                }

                submission.ItemOutcomes = byId.Values.ToArray();
                submission.AppliedAt ??= now;
                CloseOut(submission, caller.Subject);
                break;
        }

        EndInProgress(submission);
        RefreshCounts(submission);
        AppendHistory(submission, caller.Subject, RecoveredAction);
        var etag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (etag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        logger.LogWarning("Admin '{Admin}' recovered stale Applying submission '{SubmissionId}' to {Status}.",
            caller.Subject, submissionId, submission.Status);
        return new(DiscoverySubmissionCommandStatus.Ok, ETag: etag, SubmissionStatus: submission.Status,
            Diff: CurrentDiff(submission), ItemOutcomes: submission.ItemOutcomes);
    }

    private void CloseOut(DiscoverySubmission submission, string actor)
    {
        submission.Status = DiscoverySubmissionRules.DeriveStatus(submission.ItemOutcomes!);
        submission.ApplyError = submission.Status == DiscoverySubmissionStatus.Applied
            ? null
            : "One or more results failed to ingest; retry or abandon them.";
        RefreshCounts(submission);
        AppendHistory(submission, actor, HistoryActionFor(submission.Status));
    }

    private async Task<DiscoverySubmissionCommandResult> Finish(DiscoverySubmission submission, string etag,
        int historyAtClaim, DiscoverySubmissionOperation? operation, SupervisionCaller caller,
        DiscoverySubmitOutcome? outcome, CancellationToken c)
    {
        RefreshCounts(submission);
        var finalETag = await Persist(submission, etag, historyAtClaim, operation, c);
        if (finalETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.ApplyFailed, OutcomeNotRecorded, null, null,
                CurrentDiff(submission), outcome, ItemOutcomes: submission.ItemOutcomes);
        }

        logger.LogInformation("Supervisor '{Supervisor}' acted on submission '{SubmissionId}': {Status}.",
            caller.Subject, submission.Id, submission.Status);

        return new(
            submission.Status == DiscoverySubmissionStatus.ApplyFailed
                ? DiscoverySubmissionCommandStatus.ApplyFailed
                : DiscoverySubmissionCommandStatus.Ok,
            submission.ApplyError, finalETag, submission.Status, CurrentDiff(submission), outcome,
            ItemOutcomes: submission.ItemOutcomes);
    }

    public const int MaxFinalWriteAttempts = 3;

    public const string OutcomeNotRecorded =
        "Outcome could not be recorded because the submission changed concurrently; reload it (an admin can recover a stuck Applying submission).";

    /// <summary>
    /// Writes the operation's result. On a 412 it re-reads and, only while the stored submission is still held by
    /// <paramref name="operation"/> (or, with no operation, unchanged in status), re-applies our result onto it, keeping
    /// history entries others added since the claim. Bounded; returns <c>null</c> if it cannot be recorded.
    /// </summary>
    private async Task<string?> Persist(DiscoverySubmission submission, string etag, int historyAtClaim,
        DiscoverySubmissionOperation? operation, CancellationToken c)
    {
        var ours = submission.History.Skip(historyAtClaim).ToArray();
        var currentETag = etag;
        for (var attempt = 1; attempt <= MaxFinalWriteAttempts; attempt++)
        {
            var written = await discoverySubmissionsRepository.TryReplace(submission, currentETag, c);
            if (written != null)
            {
                return written;
            }

            var fresh = await discoverySubmissionsRepository.GetById(submission.Id, c);
            // Only merge onto a submission still held by this operation; anything else is a genuine conflict.
            var stillOurs = operation != null && fresh != null &&
                            fresh.Status == DiscoverySubmissionStatus.Applying && fresh.InProgressOperation == operation;
            if (!stillOurs || fresh!.ETag == null)
            {
                break;
            }

            submission.History = [.. fresh.History, .. ours.Where(o => !fresh.History.Any(h =>
                h.At == o.At && h.Action == o.Action && h.ActorSubject == o.ActorSubject))];
            currentETag = fresh.ETag;
            logger.LogWarning("Submission '{SubmissionId}' final write got 412 (attempt {Attempt}); merged and retrying.",
                submission.Id, attempt);
        }

        logger.LogError("Submission '{SubmissionId}': outcome {Status} could not be recorded.", submission.Id,
            submission.Status);
        return null;
    }

    private void BeginInProgress(DiscoverySubmission submission, DiscoverySubmissionOperation operation, string actor,
        Guid[]? resultIds)
    {
        submission.StatusBeforeInProgress = submission.Status;
        submission.Status = DiscoverySubmissionStatus.Applying;
        submission.InProgressOperation = operation;
        submission.InProgressSince = timeProvider.GetUtcNow().UtcDateTime;
        submission.InProgressBy = actor;
        submission.InProgressResultIds = resultIds;
    }

    private static void EndInProgress(DiscoverySubmission submission)
    {
        submission.InProgressOperation = null;
        submission.InProgressSince = null;
        submission.InProgressBy = null;
        submission.InProgressResultIds = null;
        submission.StatusBeforeInProgress = null;
    }

    /// <summary>
    /// Records per-result outcomes for <paramref name="targetIds"/>. Results the ingest never reached are Failed
    /// with a reason, so they can be retried.
    /// </summary>
    private static void MergeOutcomes(DiscoverySubmission submission, IEnumerable<Guid> targetIds,
        DiscoverySubmitOutcome outcome, DateTime now, bool aborted)
    {
        var byId = (submission.ItemOutcomes ?? []).ToDictionary(x => x.ResultId);
        foreach (var id in targetIds)
        {
            if (!byId.TryGetValue(id, out var item))
            {
                item = new DiscoverySubmissionItemOutcome { ResultId = id };
                byId[id] = item;
            }

            var attempt = outcome.Results.LastOrDefault(x => x.DiscoveryItemId == id);
            item.LastAttemptAt = now;
            if (attempt == null)
            {
                item.State = DiscoverySubmissionItemState.Failed;
                item.Error = aborted
                    ? "Not attempted: the run stopped before this result was submitted."
                    : "Result not found in its report.";
                continue;
            }

            item.Attempts++;
            item.EpisodeId = attempt.EpisodeId;
            item.PodcastId = attempt.PodcastId;
            item.Message = attempt.Message;
            var failedAttempt = attempt.Message == DiscoverySubmitItemOutcomeMessages.Error;
            item.State = failedAttempt ? DiscoverySubmissionItemState.Failed : DiscoverySubmissionItemState.Succeeded;
            item.Error = failedAttempt ? attempt.Error ?? "Submit failed." : null;
        }

        submission.ItemOutcomes = byId.Values.ToArray();
    }

    private static void RefreshCounts(DiscoverySubmission submission)
    {
        if (submission.ItemOutcomes == null)
        {
            return;
        }

        submission.ErroredResultIds = submission.ItemOutcomes
            .Where(x => x.State == DiscoverySubmissionItemState.Failed).Select(x => x.ResultId).ToArray();
        submission.AppliedResultCount =
            submission.ItemOutcomes.Count(x => x.State == DiscoverySubmissionItemState.Succeeded);
    }

    private static string HistoryActionFor(DiscoverySubmissionStatus status) => status switch
    {
        DiscoverySubmissionStatus.Applied => AppliedAction,
        DiscoverySubmissionStatus.PartiallyApplied => PartiallyAppliedAction,
        _ => ApplyFailedAction
    };

    private async Task<(DiscoverySubmission? Submission, DiscoverySubmissionCommandResult? Refusal)> LoadReviewable(
        Guid submissionId, string etag, SupervisionCaller caller, CancellationToken c,
        DiscoverySubmission? alreadyLoaded = null)
    {
        var submission = alreadyLoaded ?? await discoverySubmissionsRepository.GetById(submissionId, c);
        if (submission == null)
        {
            return (null, new(DiscoverySubmissionCommandStatus.NotFound));
        }

        if (!DiscoverySubmissionRules.CanReview(caller, submission))
        {
            return (null, new(DiscoverySubmissionCommandStatus.Forbidden));
        }

        if (!DiscoverySubmissionRules.IsReviewable(submission))
        {
            return (null, new(DiscoverySubmissionCommandStatus.Conflict, $"Submission is {submission.Status}."));
        }

        if (string.IsNullOrWhiteSpace(etag) || etag != submission.ETag)
        {
            return (null, new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry."));
        }

        return (submission, null);
    }

    private static void SetFinalSet(DiscoverySubmission submission, IEnumerable<Guid> finalSet)
    {
        var final = finalSet.Distinct().ToArray();
        var diff = DiscoverySubmissionRules.ComputeDiff(submission.SubmitterAcceptedResultIds, final);
        submission.SupervisorAcceptedResultIds = final;
        submission.AddedBySupervisorResultIds = diff.Added;
        submission.RemovedBySupervisorResultIds = diff.Removed;
    }

    private static DiscoverySubmissionDiff? CurrentDiff(DiscoverySubmission s) =>
        s.AddedBySupervisorResultIds == null || s.RemovedBySupervisorResultIds == null
            ? null
            : new DiscoverySubmissionDiff(s.AddedBySupervisorResultIds, s.RemovedBySupervisorResultIds);

    private void AppendHistory(DiscoverySubmission submission, string actor, string action) =>
        submission.History =
        [
            .. submission.History,
            new DiscoverySubmissionHistoryEntry { At = timeProvider.GetUtcNow().UtcDateTime, ActorSubject = actor, Action = action }
        ];

    private static bool TooLong(string? comment) => comment is { Length: > MaxCommentLength };
}
