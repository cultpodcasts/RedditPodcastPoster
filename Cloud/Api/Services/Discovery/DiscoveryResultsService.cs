using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Discovery;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IDiscoveryResultDeduplicator discoveryResultDeduplicator,
    IDiscoveryInfoContentPublisher discoveryInfoContentPublisher,
    ILogger<DiscoveryResultsService> logger) : IDiscoveryResultsService
{
    public async Task<DiscoveryCurationData> Get(bool includeHidden, CancellationToken c)
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
        var podcastsLookup = await GetPodcastInfo(results, c);
        var awaitingReview = await discoveryResultsRepository.GetAllPendingSupervisor()
            .Select(x => new DiscoveryAwaitingReview(x.Id, x.DiscoveryBegan, x.DiscoveryResults.Count()))
            .ToListAsync(c);
        var result = new DiscoveryCurationData(
            documents.Select(x => x.Id),
            results,
            podcastsLookup,
            hiddenCount,
            awaitingReview);
        logger.LogInformation("{Method} gathered {ResultCount} results ({HiddenCount} auto-hidden).",
            nameof(Get), result.Results.Count, hiddenCount);
        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, DiscoveryPodcastInfo>> GetPodcastInfo(
        IEnumerable<DiscoveryResult> results, CancellationToken c)
    {
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

        logger.LogInformation("{Method} obtained matching podcasts.", nameof(GetPodcastInfo));
        return referencedPodcasts
            .ToDictionary(
                pd => pd.id,
                pd => new DiscoveryPodcastInfo(
                    pd.name,
                    pd.isVisible,
                    visibleEpisodeCounts.TryGetValue(pd.id, out var count) ? count : 0));
    }

    public async Task<IReadOnlyList<DiscoveryResultsDocument>> GetDocuments(IEnumerable<Guid> documentIds)
    {
        return await discoveryResultsRepository.GetByIds(documentIds).ToListAsync();
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

    public async Task ReleaseClaim(Guid[] documentIds)
    {
        foreach (var id in documentIds.Distinct())
        {
            await discoveryResultsRepository.TryTransitionState(id,
                DiscoveryResultsDocumentState.Processed, DiscoveryResultsDocumentState.Unprocessed,
                CancellationToken.None);
        }
    }

    public Task<IReadOnlyList<Guid>> TryClaimForProcessing(Guid[] documentIds,
        CancellationToken cancellationToken = default) =>
        TryClaim(DiscoveryResultsDocumentState.Unprocessed, documentIds, cancellationToken);

    public Task<IReadOnlyList<Guid>> TryClaimHeldForProcessing(Guid[] documentIds,
        CancellationToken cancellationToken = default) =>
        TryClaim(DiscoveryResultsDocumentState.PendingSupervisor, documentIds, cancellationToken);

    public async Task ReleaseHeldClaim(Guid[] documentIds)
    {
        foreach (var id in documentIds.Distinct())
        {
            if (!await discoveryResultsRepository.TryTransitionState(id, DiscoveryResultsDocumentState.Processed,
                    DiscoveryResultsDocumentState.PendingSupervisor, CancellationToken.None))
            {
                logger.LogError("Failure returning report '{DocumentId}' to PendingSupervisor after failed apply.", id);
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> TryClaim(DiscoveryResultsDocumentState fromState, Guid[] documentIds,
        CancellationToken cancellationToken)
    {
        var claimed = new List<Guid>();
        foreach (var documentId in documentIds.Distinct())
        {
            if (await discoveryResultsRepository.TryTransitionState(documentId,
                    fromState, DiscoveryResultsDocumentState.Processed,
                    cancellationToken))
            {
                claimed.Add(documentId);
                continue;
            }

            if (fromState == DiscoveryResultsDocumentState.PendingSupervisor)
            {
                await ReleaseHeldClaim(claimed.ToArray());
            }
            else
            {
                await ReleaseClaim(claimed.ToArray());
            }
            return [documentId];
        }

        return [];
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
            else if (document.State == DiscoveryResultsDocumentState.PendingSupervisor)
            {
                // Only a supervisor applying the held submission may promote a held report.
                logger.LogWarning(
                    "{DiscoveryResultsDocumentName} with id '{DocumentId}' is awaiting supervisor review; not stamping.",
                    nameof(DiscoveryResultsDocument), documentId);
            }
            else
            {
                // Report was claimed (Unprocessed -> Processed) by TryClaimForProcessing; stamp result states.
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
