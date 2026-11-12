namespace Api.Models;

public enum NewsOrganisationDeleteStatus
{
    Deleted,
    NotFound,

    /// <summary>The organisation still has reports (removed ones included), so nothing was deleted.</summary>
    HasReports,
    Failed
}

/// <param name="RemainingReportCount">For <see cref="NewsOrganisationDeleteStatus.HasReports"/>: how many are left.</param>
public record NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus Status, int? RemainingReportCount = null);
