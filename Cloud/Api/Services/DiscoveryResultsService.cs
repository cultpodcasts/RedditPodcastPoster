using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IDiscoveryResultDeduplicator discoveryResultDeduplicator,
    IDiscoveryInfoContentPublisher discoveryInfoContentPublisher,
    ILogger<DiscoveryResultsService> logger) : IDiscoveryResultsService
{
    public async Task<DiscoveryResponse> Get(bool includeHidden, CancellationToken c)
    {
        logger.LogInformation("{Method} initiated. IncludeHidden={IncludeHidden}.", nameof(Get), includeHidden);
        var documents = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync(c);
        logger.LogInformation("{Method} obtained unprocessed documents.", nameof(Get));
        var allResults = discoveryResultDeduplicator.Deduplicate(
            documents.SelectMany(x => x.DiscoveryResults));
        var hiddenCount = allResults.Count(x => x.AutoHidden);
        var visibleResults = includeHidden
            ? allResults
            : allResults.Where(x => !x.AutoHidden);
        var results = visibleResults.ToList();
        var podcastIds = results.SelectMany(x => x.MatchingPodcastIds).Distinct().ToArray();

        IReadOnlyList<(Guid id, string name, bool isVisible)> referencedPodcasts = podcastIds.Length == 0
            ? []
            : await podcastRepository
                .GetAllBy(x => Enumerable.Contains(podcastIds, x.Id))
                .Select(podcast => (podcast.Id, podcast.Name, podcast.Removed != true))
                .ToListAsync(c);

        var visibleEpisodeCounts = new Dictionary<Guid, int>();
        if (podcastIds.Length > 0)
        {
            await foreach (var episode in episodeRepository
                               .GetAllBy(x => Enumerable.Contains(podcastIds, x.PodcastId) && !x.Removed)
                               .WithCancellation(c))
            {
                if (!visibleEpisodeCounts.TryAdd(episode.PodcastId, 1))
                {
                    visibleEpisodeCounts[episode.PodcastId]++;
                }
            }
        }

        logger.LogInformation($"{nameof(Get)} Obtained matching podcasts.");
        var podcastsLookup = referencedPodcasts
            .ToDictionary(pd => pd.id, pd => new DiscoveryPodcast
            {
                Name = pd.name,
                IsVisible = pd.isVisible,
                VisibleEpisodes = visibleEpisodeCounts.TryGetValue(pd.id, out var count) ? count : 0
            });
        var result = new DiscoveryResponse
        {
            Ids = documents.Select(x => x.Id),
            Results = results
                .Select(x => x.ToDiscoveryResponseItem(podcastsLookup))
                .OrderByDescending(x => x.AcceptProbability ?? -1f)
                .ThenBy(x => x.Released),
            HiddenCount = hiddenCount
        };
        logger.LogInformation("{Method} gathered {ResultCount} results ({HiddenCount} auto-hidden).",
            nameof(Get), result.Results.Count(), hiddenCount);
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
            await discoveryInfoContentPublisher.PublishUnprocessedSummaryAsync();
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
                logger.LogError("No {DiscoveryResultsDocumentName} with id '{DocumentId}'.",
                    nameof(DiscoveryResultsDocument), documentId);
            }
            else if (document.State != DiscoveryResultsDocumentState.Unprocessed)
            {
                logger.LogWarning(
                    "{DiscoveryResultsDocumentName} with id '{DocumentId}' is not in unprocessed-state. Has state '{DiscoveryResultsDocumentState}'.",
                    nameof(DiscoveryResultsDocument), documentId, document.State);
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