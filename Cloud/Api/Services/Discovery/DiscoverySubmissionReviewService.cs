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

        // Claim the apply with the caller's ETag so a concurrent apply (or edit) cannot run twice.
        SetFinalSet(submission, finalSet);
        submission.SupervisorSubject = caller.Subject;
        submission.ApplyError = null;
        AppendHistory(submission, caller.Subject, ApplyStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

        var result = await discoveryCurationSubmitService.ApplyHeldAsync(new DiscoverySubmitRequest
        {
            DiscoveryResultsDocumentIds = submission.DiscoveryResultsDocumentIds,
            ResultIds = finalSet
        }, c);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (result.Status == DiscoveryCurationSubmitStatus.Conflict)
        {
            // The guarded PendingSupervisor -> Processed claim failed before any ingest; nothing happened.
            AppendHistory(submission, caller.Subject, ApplyConflictAction);
            await discoverySubmissionsRepository.TryReplace(submission, claimedETag, c);
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

        return await Finish(submission, claimedETag, caller, result.Outcome, c);
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
            return await Finish(submission, request.ETag, caller, null, c);
        }

        AppendHistory(submission, caller.Subject, RetryStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

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

        CloseOut(submission, caller.Subject);
        return await Finish(submission, claimedETag, caller, result.Outcome, c);
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

        AppendHistory(submission!, caller.Subject, ReleaseStartedAction);
        var claimedETag = await discoverySubmissionsRepository.TryReplace(submission!, request.ETag, c);
        if (claimedETag == null)
        {
            return new(DiscoverySubmissionCommandStatus.Conflict, "Submission changed; reload and retry.");
        }

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
        AppendHistory(submission, caller.Subject, ReleasedAction);
        var finalETag = await discoverySubmissionsRepository.TryReplace(submission, claimedETag, c);
        await discoveryResultsService.UpdateDiscoveryInfoContent();
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
            .Where(x => DiscoverySubmissionRules.IsReviewable(x) && x.DiscoveryResultsDocumentIds.Contains(documentId))
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
        SupervisionCaller caller, DiscoverySubmitOutcome? outcome, CancellationToken c)
    {
        RefreshCounts(submission);
        var finalETag = await discoverySubmissionsRepository.TryReplace(submission, etag, c);
        if (finalETag == null)
        {
            logger.LogError("Submission '{SubmissionId}' changed concurrently; outcome {Status} not recorded.",
                submission.Id, submission.Status);
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
