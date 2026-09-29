using Api.Models;

namespace Api.Dtos.Extensions;

public static class PodcastKindTransferResponseExtension
{
    public static PodcastKindTransferResponse ToDto(this PodcastKindTransferResult result) =>
        new()
        {
            ParentId = result.ParentId,
            TargetKind = result.TargetKind,
            PlayableCount = result.PlayableCount,
            FailureIndexingPlayables = result.FailureIndexingPlayables
        };
}
