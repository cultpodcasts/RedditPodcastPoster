using Api.Models;

namespace Api.Services.SearchIndex;

public interface ISearchIndexRunService
{
    Task<SearchIndexRunResult> RunAsync(CancellationToken cancellationToken);
}
