using Api.Dtos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;

namespace Api.Services;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    ILogger<DiscoveryResultsService> logger) : IDiscoveryResultsService
{
    public async Task<DiscoveryResponse> Get(CancellationToken c)
    {
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(c);
        var results = documents.SelectMany(x => x.DiscoveryResults);
        var result = new DiscoveryResponse
        {
            Ids = documents.Select(x => x.Id),
            Results = results.OrderBy(x => x.Released)
        };
        return result;
    }

    public async Task MarkAsProcessed(Guid[] documentIds)
    {
        foreach (var documentId in documentIds)
        {
            var document = await discoveryResultsRepository.GetById(documentId);
            if (document == null)
            {
                logger.LogError($"No {nameof(DiscoveryResultsDocument)} with id '{documentId}'.");
            }
            else if (document.State != DiscoveryResultState.Unprocessed)
            {
                logger.LogWarning(
                    $"{nameof(DiscoveryResultsDocument)} with id '{documentId}' is not in unprocessed-state. Has state '{document.State}'.");
            }
            else if (document.State == DiscoveryResultState.Unprocessed)
            {
                document.State = DiscoveryResultState.Processed;
                await discoveryResultsRepository.Save(document);
            }
        }
    }

    public async Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest)
    {
        var documentResultSets = await discoveryResultsRepository
            .GetByIds(discoverySubmitRequest.DiscoveryResultsDocumentIds)
            .ToListAsync();
        var discoveryResults = documentResultSets.SelectMany(x => x.DiscoveryResults);
        return discoveryResults.Where(y => discoverySubmitRequest.ResultIds.Contains(y.Id));
    }
}