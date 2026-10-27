using System.Linq.Expressions;
using RedditPodcastPoster.Models.News;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface INewsReportRepository : IRepository<NewsReport>, IFilterableRepository<NewsReport>
{
    Task<NewsReport?> GetNewsReport(Guid newsOrganisationId, Guid reportId);
    Task<int> Count(Guid newsOrganisationId);
    IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(Guid newsOrganisationId);
    IAsyncEnumerable<NewsReport> GetByNewsOrganisationId(
        Guid newsOrganisationId,
        Expression<Func<NewsReport, bool>> selector);
    Task Save(IEnumerable<NewsReport> reports);
    Task Delete(Guid newsOrganisationId, Guid reportId);

    /// <summary>Point read that also returns the ETag; null when the report does not exist.</summary>
    Task<ETagged<NewsReport>?> GetNewsReportWithETag(Guid newsOrganisationId, Guid reportId);

    /// <summary>
    /// Cosmos partial patch of <c>/services</c> and/or <c>/youTubeId</c> only, guarded by <paramref name="ifMatchETag"/>.
    /// A missing document is <see cref="CataloguePlayablePatchOutcome.NotFound"/> (never recreated); a changed one is
    /// <see cref="CataloguePlayablePatchOutcome.Conflict"/>.
    /// </summary>
    Task<CataloguePlayablePatchOutcome> PatchFields(
        Guid newsOrganisationId,
        Guid reportId,
        string ifMatchETag,
        CataloguePlayableFieldPatch patch);
}
