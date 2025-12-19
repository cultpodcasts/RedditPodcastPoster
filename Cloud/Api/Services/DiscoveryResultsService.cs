using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Api.Services;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    IContentPublisher contentPublisher,
    ILogger<DiscoveryResultsService> logger) : IDiscoveryResultsService
{
    public async Task<DiscoveryResponse> Get(CancellationToken c)
    {
        logger.LogInformation($"{nameof(Get)} initiated.");
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(c);
        logger.LogInformation($"{nameof(Get)} Obtained unprocessed documents.");
        var results = documents.SelectMany(x => x.DiscoveryResults);
        var podcastIds = results.SelectMany(x => x.MatchingPodcastIds).Distinct();
        var referencedPodcasts = await podcastRepository
            .GetAllBy(x =>
                podcastIds.Contains(x.Id), podcast => new {id = podcast.Id, name = podcast.Name})
            .ToListAsync(c);
        logger.LogInformation($"{nameof(Get)} Obtained matching podcasts.");
        var podcastsLookup = referencedPodcasts
            .ToDictionary(pd => pd.id, pd => pd.name);
        var result = new DiscoveryResponse
        {
            Ids = documents.Select(x => x.Id),
            Results = results.Select(x => x.ToDiscoveryResponseItem(podcastsLookup))
                .OrderBy(x => x.Released)
        };
        logger.LogInformation($"{nameof(Get)} Gathered results.");
        return result;
    }

    public async Task<IEnumerable<DiscoveryResult>> GetDiscoveryResult(DiscoverySubmitRequest discoverySubmitRequest)
    {
        var documentResultSets = await discoveryResultsRepository
            .GetByIds(discoverySubmitRequest.DiscoveryResultsDocumentIds)
            .ToListAsync();
        var discoveryResults = documentResultSets.SelectMany(x => x.DiscoveryResults);
        return discoveryResults.Where(y => discoverySubmitRequest.ResultIds.Contains(y.Id));
    }

    public async Task UpdateDiscoveryInfoContent()
    {
        try
        {
            var unprocessedDiscoveryReports = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync();
            var numberOfReports = unprocessedDiscoveryReports.Count();
            DateTime? minProcessed= null;
            int? numberOfResults = null;
            if (numberOfReports > 0)
            {
                minProcessed = unprocessedDiscoveryReports.Min(x => x.DiscoveryBegan);
                numberOfResults = unprocessedDiscoveryReports.SelectMany(x => x.DiscoveryResults).Count();
            }
            await contentPublisher.PublishDiscoveryInfo(new DiscoveryInfo
            {
                DocumentCount = numberOfReports,
                NumberOfResults = numberOfResults,
                DiscoveryBegan = minProcessed
            });
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure to update discover-info-content.");
        }
    }

    public async Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds)
    {
        foreach (var documentId in documentIds)
        {
            var document = await discoveryResultsRepository.GetById(documentId);
            if (document == null)
            {
                logger.LogError("No {DiscoveryResultsDocumentName} with id '{DocumentId}'.", nameof(DiscoveryResultsDocument), documentId);
            }
            else if (document.State != DiscoveryResultsDocumentState.Unprocessed)
            {
                logger.LogWarning(
                    "{DiscoveryResultsDocumentName} with id '{DocumentId}' is not in unprocessed-state. Has state '{DiscoveryResultsDocumentState}'.", nameof(DiscoveryResultsDocument), documentId, document.State);
            }
            else if (document.State == DiscoveryResultsDocumentState.Unprocessed)
            {
                document.State = DiscoveryResultsDocumentState.Processed;

                foreach (var documentDiscoveryResult in document.DiscoveryResults)
                {
                    if (erroredResultIds.Contains(documentDiscoveryResult.Id))
                    {
                        documentDiscoveryResult.State = DiscoveryResultState.AcceptError;
                    }
                    else if (acceptedResultIds.Contains(documentDiscoveryResult.Id))
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
}