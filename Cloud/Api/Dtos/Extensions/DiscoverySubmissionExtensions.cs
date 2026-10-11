using Api.Models;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Dtos.Extensions;

public static class DiscoverySubmissionExtensions
{
    public static DiscoverySubmissionListResponse ToDto(this DiscoverySubmissionList list) => new()
    {
        View = list.IsSupervisorView ? "supervisor" : "submitter",
        PendingCount = list.PendingCount,
        Submissions = list.Submissions.Select(ToSummaryDto).ToArray()
    };

    public static DiscoverySubmissionSummaryDto ToSummaryDto(this DiscoverySubmission s) => new()
    {
        Id = s.Id,
        Status = s.Status.ToString(),
        SubmitterSubject = s.SubmitterSubject,
        SubmittedAt = s.SubmittedAt,
        DiscoveryResultsDocumentIds = s.DiscoveryResultsDocumentIds,
        SubmitterAcceptedCount = s.SubmitterAcceptedResultIds.Length,
        SupervisorSubject = s.SupervisorSubject,
        AppliedAt = s.AppliedAt,
        ReportComment = s.ReportComment,
        ItemCommentCount = s.ItemComments?.Length ?? 0,
        ApplyError = s.ApplyError,
        FailedCount = s.ItemOutcomes?.Count(x => x.State == DiscoverySubmissionItemState.Failed),
        SucceededCount = s.ItemOutcomes?.Count(x => x.State == DiscoverySubmissionItemState.Succeeded),
        AbandonedCount = s.ItemOutcomes?.Count(x => x.State == DiscoverySubmissionItemState.Abandoned),
        ReleasedAt = s.ReleasedAt,
        ReleaseReason = s.ReleaseReason
    };

    public static DiscoverySubmissionReleaseRequest ToModel(this DiscoverySubmissionReleaseRequestDto dto) =>
        new() { ETag = dto.ETag, Reason = dto.Reason };

    public static DiscoveryReportReleaseRequest ToModel(this DiscoveryReportReleaseRequestDto dto) =>
        new() { Reason = dto.Reason };

    public static DiscoverySubmissionItemOutcomeDto[]? ToDtos(this IEnumerable<DiscoverySubmissionItemOutcome>? outcomes) =>
        outcomes?.Select(x => new DiscoverySubmissionItemOutcomeDto
        {
            ResultId = x.ResultId,
            State = x.State.ToString(),
            EpisodeId = x.EpisodeId,
            PodcastId = x.PodcastId,
            Message = x.Message,
            Error = x.Error,
            Attempts = x.Attempts,
            LastAttemptAt = x.LastAttemptAt
        }).ToArray();

    public static DiscoverySubmissionRetryRequest ToModel(this DiscoverySubmissionRetryRequestDto dto) => new()
    {
        ETag = dto.ETag,
        AbandonResultIds = dto.AbandonResultIds
    };

    public static DiscoverySubmissionDetailResponse ToDto(this DiscoverySubmissionDetail detail)
    {
        var s = detail.Submission;
        var podcasts = detail.Podcasts.ToDictionary(
            kv => kv.Key,
            kv => new DiscoveryResponse.Item.MatchingPodcast
            {
                Name = kv.Value.Name,
                IsVisible = kv.Value.IsVisible,
                VisibleEpisodes = kv.Value.VisibleEpisodes
            });
        return new DiscoverySubmissionDetailResponse
        {
            Id = s.Id,
            Status = s.Status.ToString(),
            ETag = s.ETag ?? "",
            CanReview = detail.CanReview,
            SubmitterSubject = s.SubmitterSubject,
            SubmittedAt = s.SubmittedAt,
            SubmitterAcceptedResultIds = s.SubmitterAcceptedResultIds,
            SupervisorAcceptedResultIds = s.SupervisorAcceptedResultIds,
            AddedBySupervisorResultIds = s.AddedBySupervisorResultIds,
            RemovedBySupervisorResultIds = s.RemovedBySupervisorResultIds,
            ReportComment = s.ReportComment,
            ItemComments = (s.ItemComments ?? [])
                .Select(x => new DiscoverySubmissionItemCommentDto { ResultId = x.ResultId, Comment = x.Comment })
                .ToArray(),
            SupervisorSubject = s.SupervisorSubject,
            AppliedAt = s.AppliedAt,
            ApplyError = s.ApplyError,
            ErroredResultIds = s.ErroredResultIds,
            ItemOutcomes = s.ItemOutcomes.ToDtos(),
            ReleasedAt = s.ReleasedAt,
            ReleaseReason = s.ReleaseReason,
            Reports = detail.Reports.Select(r => new DiscoverySubmissionReportDto
            {
                Id = r.Id,
                DiscoveryBegan = r.DiscoveryBegan,
                State = r.State.ToString(),
                Results = r.DiscoveryResults.Select(x => x.ToDiscoveryResponseItem(podcasts)).ToArray()
            }).ToArray(),
            History = s.History.Select(h => new DiscoverySubmissionHistoryDto
            {
                At = h.At,
                ActorSubject = h.ActorSubject,
                Action = h.Action
            }).ToArray()
        };
    }

    public static DiscoverySubmissionReviewRequest ToModel(this DiscoverySubmissionReviewRequestDto dto) => new()
    {
        ETag = dto.ETag,
        ReportComment = dto.ReportComment,
        ItemComments = dto.ItemComments?.Select(x => new DiscoverySubmissionItemCommentModel(x.ResultId, x.Comment ?? ""))
            .ToArray(),
        AcceptedResultIds = dto.AcceptedResultIds
    };

    public static DiscoverySubmissionApplyRequest ToModel(this DiscoverySubmissionApplyRequestDto dto) => new()
    {
        ETag = dto.ETag,
        AcceptedResultIds = dto.AcceptedResultIds
    };

    public static DiscoverySubmissionCommandResponse ToDto(this DiscoverySubmissionCommandResult result,
        Guid submissionId) => new()
    {
        SubmissionId = submissionId,
        Status = result.SubmissionStatus?.ToString() ?? result.Status.ToString(),
        ETag = result.ETag,
        AddedBySupervisorResultIds = result.Diff?.Added,
        RemovedBySupervisorResultIds = result.Diff?.Removed,
        Message = result.Message,
        Ids = result.InvalidIds?.ToArray(),
        Outcome = result.Outcome?.ToDto(),
        ItemOutcomes = result.ItemOutcomes.ToDtos()
    };
}
