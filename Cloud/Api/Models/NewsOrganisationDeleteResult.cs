namespace Api.Models;

public enum NewsOrganisationDeleteStatus
{
    Deleted,
    NotFound,

    /// <summary>The organisation still has reports (removed ones included), so nothing was deleted.</summary>
    HasReports,

    /// <summary>The organisation is being indexed; nothing was deleted, retry shortly.</summary>
    Busy,

    /// <summary>A bad or unsafe <c>redirectToNewsOrganisationId</c>; nothing was deleted (<c>Error</c> says why).</summary>
    BadRequest,
    Failed
}

/// <param name="RemainingReportCount">For <see cref="NewsOrganisationDeleteStatus.HasReports"/>: how many are left.</param>
public record NewsOrganisationDeleteResult(NewsOrganisationDeleteStatus Status, int? RemainingReportCount = null)
{
    /// <summary>For <see cref="NewsOrganisationDeleteStatus.BadRequest"/>: why.</summary>
    public string? Error { get; init; }

    /// <summary>The name the old name route now redirects to, when the delete recorded a redirect.</summary>
    public string? RedirectedToName { get; init; }
}
