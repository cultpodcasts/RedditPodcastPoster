using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeAwaitingApprovalService
{
    /// <summary>
    /// Episodes whose approval status is awaiting approval (#1491), newest release first.
    /// </summary>
    Task<EpisodeAwaitingApprovalResult> GetAwaitingApprovalAsync(CancellationToken cancellationToken);
}
