using API.Dtos;

namespace API.Data;

public interface IQueryExecutor
{
    Task<HomePageModel> GetHomePage(CancellationToken ct);
}