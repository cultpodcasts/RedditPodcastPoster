using Api.Models;

namespace Api.Dtos.Extensions;

public static class PodcastKindTransferJobStatusResponseExtension
{
    public static PodcastKindTransferJobStatusResponse ToDto(this PodcastKindTransferJobStatusResult result) =>
        new()
        {
            Status = result.Status.ToString(),
            ParentId = result.ParentId,
            TargetKind = result.TargetKind,
            PlayableCount = result.PlayableCount,
            FailureIndexingPlayables = result.FailureIndexingPlayables,
            FailureReason = result.FailureReason,
            EnqueuedUtc = result.EnqueuedUtc,
            StartedUtc = result.StartedUtc,
            CompletedUtc = result.CompletedUtc
        };
}
