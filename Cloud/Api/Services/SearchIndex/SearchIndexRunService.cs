using Api.Extensions;
using Api.Models;
using Microsoft.Extensions.Logging;
using IndexerState = RedditPodcastPoster.Search.Models.IndexerState;
using RedditPodcastPoster.Search.Services;

namespace Api.Services.SearchIndex;

public interface ISearchIndexRunService
{
    Task<SearchIndexRunResult> RunAsync(CancellationToken cancellationToken);
}

public class SearchIndexRunService(
    ISearchIndexerService searchIndexerService,
    ILogger<SearchIndexRunService> logger) : ISearchIndexRunService
{
    public async Task<SearchIndexRunResult> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await searchIndexerService.RunIndexer();
            var dto = result.ToDto();
            return result.IndexerState == IndexerState.Executed
                ? new SearchIndexRunResult(SearchIndexRunStatus.Ok, dto)
                : new SearchIndexRunResult(SearchIndexRunStatus.BadRequest, dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(RunAsync)}: Failed to run indexer.");
            return new SearchIndexRunResult(SearchIndexRunStatus.Failed);
        }
    }
}
