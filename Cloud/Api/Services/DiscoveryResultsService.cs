using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Api.Services;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    ILogger<DiscoveryResultsService> logger) : IDiscoveryResultsService
{
    public async Task<DiscoveryResponse> Get(CancellationToken c)
    {
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(c);
        var results = documents.SelectMany(x => x.DiscoveryResults);
        var podcastIds = results.SelectMany(x => x.MatchingPodcastIds).Distinct();
        var referencedPodcasts = await podcastRepository
            .GetAllBy(x =>
                podcastIds.Contains(x.Id), podcast => new {id = podcast.Id, name = podcast.Name})
            .ToListAsync(c);
        var podcastsLookup = referencedPodcasts
            .ToDictionary(pd => pd.id, pd => pd.name);
        var result = new DiscoveryResponse
        {
            Ids = documents.Select(x => x.Id),
            Results = results.Select(x => { return x.ToDiscoveryResponseItem(podcastsLookup); })
                .OrderBy(x => x.Released)
        };
        return result;
    }

    public async Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds)
    {
        foreach (var documentId in documentIds)
        {
            var document = await discoveryResultsRepository.GetById(documentId);
            if (document == null)
            {
                logger.LogError($"No {nameof(DiscoveryResultsDocument)} with id '{documentId}'.");
            }
            else if (document.State != DiscoveryResultsDocumentState.Unprocessed)
            {
                logger.LogWarning(
                    $"{nameof(DiscoveryResultsDocument)} with id '{documentId}' is not in unprocessed-state. Has state '{document.State}'.");
            }
            else if (document.State == DiscoveryResultsDocumentState.Unprocessed)
            {
                document.State = DiscoveryResultsDocumentState.Processed;

                foreach (var documentDiscoveryResult in document.DiscoveryResults)
                {
                    if (acceptedResultIds.Contains(documentDiscoveryResult.Id))
                    {
                        documentDiscoveryResult.State = DiscoveryResultState.Accepted;
                    }
                    else
                    {
                        documentDiscoveryResult.State = DiscoveryResultState.Rejected;
                    }
                }

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