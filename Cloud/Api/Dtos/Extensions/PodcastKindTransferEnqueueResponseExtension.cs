using Api.Models;

namespace Api.Dtos.Extensions;

public static class PodcastKindTransferEnqueueResponseExtension
{
    public static PodcastKindTransferEnqueueResponse ToDto(this PodcastKindTransferEnqueueResult result) =>
        new()
        {
            Status = result.Status.ToString(),
            ParentId = result.ParentId,
            TargetKind = result.TargetKind,
            EnqueuedUtc = result.EnqueuedUtc,
            StatusPath = result.ParentId is { } id
                ? $"podcast/{id:D}/kind"
                : null
        };
}
