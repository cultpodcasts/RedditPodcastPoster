using Indexer.Dtos;

namespace Indexer.Data;

public interface IQueryExecutor
{
    Task<HomePageModel> GetHomePage(CancellationToken ct);
}