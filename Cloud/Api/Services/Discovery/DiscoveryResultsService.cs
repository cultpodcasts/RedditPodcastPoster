using Microsoft.Extensions.Logging;
using Api.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Discovery;

public class DiscoveryResultsService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    INewsReportRepository newsReportRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
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
        var info = referencedPodcasts
            .ToDictionary(
                pd => pd.id,
                pd => new DiscoveryPodcastInfo(
                    pd.name,
                    pd.isVisible,
                    visibleEpisodeCounts.TryGetValue(pd.id, out var count) ? count : 0));
        var parentIds = podcastIds.Where(id => !info.ContainsKey(id)).ToArray();
        if (parentIds.Length > 0)
        {
            await AddCatalogueParentInfo(parentIds, info, c);
        }

        return info;
    }

    /// <summary>
    /// Matched ids that are not podcasts: news organisations / TV shows (a migrated podcast keeps its id). Visible
    /// items are the parent's reports / episodes that are not removed.
    /// </summary>
    private async Task AddCatalogueParentInfo(Guid[] ids, Dictionary<Guid, DiscoveryPodcastInfo> info,
        CancellationToken c)
    {
        await foreach (var organisation in newsOrganisationRepository
                           .GetAllBy(x => Enumerable.Contains(ids, x.Id)).WithCancellation(c))
        {
            var reports = await newsReportRepository.GetByNewsOrganisationId(organisation.Id)
                .CountAsync(x => !x.Removed, c);
            info[organisation.Id] = new DiscoveryPodcastInfo(organisation.Name, !organisation.IsRemoved(), reports,
                nameof(CatalogueParentKind.NewsOrganisation));
        }

        await foreach (var show in tvShowRepository
                           .GetAllBy(x => Enumerable.Contains(ids, x.Id)).WithCancellation(c))
        {
            var episodes = await tvShowEpisodeRepository.GetByTvShowId(show.Id).CountAsync(x => !x.Removed, c);
            info[show.Id] = new DiscoveryPodcastInfo(show.Name, !show.IsRemoved(), episodes,
                nameof(CatalogueParentKind.TvShow));
        }

        logger.LogInformation("{Method} obtained matching news organisations / TV shows.", nameof(GetPodcastInfo));
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

    public async Task UpdateResultStates(Guid[] documentIds, Guid[] succeededResultIds, Guid[] erroredResultIds,
        IReadOnlyList<DiscoverySubmitItemOutcome> outcomes)
    {
        var byId = OutcomeIndex(outcomes);
        foreach (var documentId in documentIds.Distinct())
        {
            var document = await discoveryResultsRepository.GetById(documentId);
            if (document is not { State: DiscoveryResultsDocumentState.Processed })
            {
                logger.LogWarning("{Method}: '{DocumentId}' is missing or not Processed; not restamping.",
                    nameof(UpdateResultStates), documentId);
                continue;
            }

            var changed = false;
            foreach (var result in document.DiscoveryResults)
            {
                if (succeededResultIds.Contains(result.Id))
                {
                    StampAccepted(result, byId.GetValueOrDefault(result.Id), logger);
                    changed = true;
                }
                else if (erroredResultIds.Contains(result.Id))
                {
                    StampAcceptError(result, byId.GetValueOrDefault(result.Id));
                    changed = true;
                }
            }

            if (changed)
            {
                await discoveryResultsRepository.Save(document);
            }
        }
    }

    public async Task<IReadOnlyList<DiscoverySubmitOutcomeItem>> GetSubmitOutcome(Guid[] documentIds,
        Guid[] resultIds, CancellationToken cancellationToken = default)
    {
        var wanted = resultIds.Distinct().ToHashSet();
        var documents = await discoveryResultsRepository.GetByIds(documentIds.Distinct()).ToListAsync(cancellationToken);
        var found = new Dictionary<Guid, DiscoverySubmitOutcomeItem>();
        foreach (var document in documents)
        {
            foreach (var result in document.DiscoveryResults)
            {
                if (!wanted.Contains(result.Id))
                {
                    continue;
                }

                found[result.Id] = ToOutcomeItem(result);
            }
        }

        return resultIds.Select(id => found.TryGetValue(id, out var item)
            ? item
            : new DiscoverySubmitOutcomeItem(id, DiscoverySubmitOutcomeState.Unknown)).ToArray();
    }

    private static DiscoverySubmitOutcomeItem ToOutcomeItem(DiscoveryResult result) =>
        result.State switch
        {
            DiscoveryResultState.Accepted => new DiscoverySubmitOutcomeItem(result.Id,
                DiscoverySubmitOutcomeState.Accepted,
                result.AcceptedPodcastId, result.AcceptedEpisodeId, null, result.EpisodeName, result.ShowName),
            DiscoveryResultState.AcceptError => new DiscoverySubmitOutcomeItem(result.Id,
                DiscoverySubmitOutcomeState.AcceptError,
                result.AcceptedPodcastId, result.AcceptedEpisodeId, result.AcceptError, result.EpisodeName,
                result.ShowName),
            DiscoveryResultState.Rejected => new DiscoverySubmitOutcomeItem(result.Id,
                DiscoverySubmitOutcomeState.Rejected,
                EpisodeName: result.EpisodeName, ShowName: result.ShowName),
            _ => new DiscoverySubmitOutcomeItem(result.Id, DiscoverySubmitOutcomeState.Pending,
                EpisodeName: result.EpisodeName, ShowName: result.ShowName)
        };

    private static Dictionary<Guid, DiscoverySubmitItemOutcome> OutcomeIndex(
        IReadOnlyList<DiscoverySubmitItemOutcome> outcomes) =>
        outcomes.GroupBy(x => x.DiscoveryItemId).ToDictionary(g => g.Key, g => g.Last());

    private static void StampAccepted(DiscoveryResult result, DiscoverySubmitItemOutcome? outcome, ILogger log)
    {
        if (outcome == null)
        {
            // Accepted without an ingest outcome would persist success with null ids — reconcile must not lie.
            log.LogWarning(
                "Accepted discovery result '{ResultId}' has no submit outcome; stamping AcceptError instead.",
                result.Id);
            StampAcceptError(result, new DiscoverySubmitItemOutcome(result.Id, null, null,
                DiscoverySubmitItemOutcomeMessages.Error, "Missing submit outcome for accepted result."));
            return;
        }

        result.State = DiscoveryResultState.Accepted;
        result.AcceptError = null;
        result.AcceptedPodcastId = outcome.PodcastId;
        result.AcceptedEpisodeId = outcome.EpisodeId;
    }

    private static void StampAcceptError(DiscoveryResult result, DiscoverySubmitItemOutcome? outcome)
    {
        result.State = DiscoveryResultState.AcceptError;
        result.AcceptedPodcastId = outcome?.PodcastId;
        result.AcceptedEpisodeId = outcome?.EpisodeId;
        result.AcceptError = outcome?.Error ?? outcome?.Message;
    }

    private static void StampRejected(DiscoveryResult result)
    {
        result.State = DiscoveryResultState.Rejected;
        result.AcceptedPodcastId = null;
        result.AcceptedEpisodeId = null;
        result.AcceptError = null;
    }

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

    public async Task MarkAsProcessed(Guid[] documentIds, Guid[] acceptedResultIds, Guid[] erroredResultIds,
        IReadOnlyList<DiscoverySubmitItemOutcome> outcomes)
    {
        var byId = OutcomeIndex(outcomes);
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
                        StampAcceptError(documentDiscoveryResult, byId.GetValueOrDefault(documentDiscoveryResult.Id));
                    }
                    else if (acceptedResultIds.Contains(documentDiscoveryResult.Id))
                    {
                        StampAccepted(documentDiscoveryResult, byId.GetValueOrDefault(documentDiscoveryResult.Id),
                            logger);
                    }
                    else
                    {
                        StampRejected(documentDiscoveryResult);
                    }
                }

                await discoveryResultsRepository.Save(document);
            }
        }
    }
}
