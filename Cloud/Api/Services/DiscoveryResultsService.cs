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
            .GetAllBy(x => podcastIds.Contains(x.Id), p => new PodcastDetails(p.Id, p.Name))
            .ToDictionaryAsync(pd => pd.Id, pd => pd.Name, c);
        var result = new DiscoveryResponse
        {
            Ids = documents.Select(x => x.Id),
            Results = results.Select(x => { return x.ToDiscoveryResponseItem(referencedPodcasts); })
                .OrderBy(x => x.Released)
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

    private record PodcastDetails(Guid Id, string Name);
}