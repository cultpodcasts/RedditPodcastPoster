using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Discovery;

public class DiscoverySubmissionReviewService(
    IDiscoverySubmissionsRepository discoverySubmissionsRepository,
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryCurationSubmitService discoveryCurationSubmitService,
    TimeProvider timeProvider,
    ILogger<DiscoverySubmissionReviewService> logger) : IDiscoverySubmissionReviewService
{
    public const string ReviewSavedAction = "ReviewSaved";
    public const string ApplyStartedAction = "ApplyStarted";
    public const string AppliedAction = "Applied";
    public const string ApplyFailedAction = "ApplyFailed";
    public const string ApplyConflictAction = "ApplyConflict";
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
        if (result.Status == DiscoveryCurationSubmitStatus.Ok)
        {
            var errored = result.Outcome!.Results.Where(x => x.Message == "Error").Select(x => x.DiscoveryItemId)
                .ToArray();
            submission.Status = DiscoverySubmissionStatus.Applied;
            submission.AppliedAt = now;
            submission.ErroredResultIds = errored;
            submission.AppliedResultCount = finalSet.Length - errored.Length;
            AppendHistory(submission, caller.Subject, AppliedAction);
        }
        else if (result.Status == DiscoveryCurationSubmitStatus.Conflict)
        {
            // The guarded PendingSupervisor -> Processed claim failed before any ingest; nothing happened.
            AppendHistory(submission, caller.Subject, ApplyConflictAction);
            await discoverySubmissionsRepository.TryReplace(submission, claimedETag, c);
            return new(DiscoverySubmissionCommandStatus.Conflict,
                "One or more reports are no longer held for this submission.",
                InvalidIds: result.UnavailableDocumentIds);
        }
        else
        {
            submission.Status = DiscoverySubmissionStatus.ApplyFailed;
            submission.ApplyError = "Ingest failed.";
            AppendHistory(submission, caller.Subject, ApplyFailedAction);
        }

        var finalETag = await discoverySubmissionsRepository.TryReplace(submission, claimedETag, c);
        if (finalETag == null)
        {
            logger.LogError("Submission '{SubmissionId}' changed during apply; outcome {Status} not recorded.",
                submissionId, submission.Status);
        }

        logger.LogInformation("Supervisor '{Supervisor}' applied submission '{SubmissionId}': {Status}.",
            caller.Subject, submissionId, submission.Status);

        return submission.Status == DiscoverySubmissionStatus.Applied
            ? new(DiscoverySubmissionCommandStatus.Ok, ETag: finalETag, SubmissionStatus: submission.Status,
                Diff: CurrentDiff(submission), Outcome: result.Outcome)
            : new(DiscoverySubmissionCommandStatus.ApplyFailed, submission.ApplyError, finalETag, submission.Status,
                CurrentDiff(submission));
    }

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
