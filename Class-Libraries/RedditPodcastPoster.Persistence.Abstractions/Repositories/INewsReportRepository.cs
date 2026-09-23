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
}
