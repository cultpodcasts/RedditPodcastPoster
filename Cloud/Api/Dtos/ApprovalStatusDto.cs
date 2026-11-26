namespace Api.Dtos;

/// <summary>
/// Curator approval on episode JSON. Names and numeric values mirror the domain approval enum
/// so string and numeric JSON stay the same.
/// Null means approved and is omitted.
/// </summary>
public enum ApprovalStatusDto
{
    AwaitingApproval,
    Rejected
}
