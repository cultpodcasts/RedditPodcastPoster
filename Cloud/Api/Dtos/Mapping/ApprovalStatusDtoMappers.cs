using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret

namespace Api.Dtos.Mapping;

/// <summary>Maps the domain approval enum onto the API enum, preserving null.</summary>
public static class ApprovalStatusDtoMappers
{
    public static ApprovalStatusDto? ToDto(this ApprovalStatus? status) =>
        status?.ToDto();

    /// <summary>Maps by name; throws on an unmapped member so enum drift fails loudly.</summary>
    public static ApprovalStatusDto ToDto(this ApprovalStatus status) =>
        status switch
        {
            ApprovalStatus.AwaitingApproval => ApprovalStatusDto.AwaitingApproval,
            ApprovalStatus.Rejected => ApprovalStatusDto.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped ApprovalStatus value.")
        };
}
