using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace Api.Services.Discovery;

public class DiscoveryCurationSubmitService(
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryUrlSubmitter discoveryUrlSubmitter,
    IEpisodeSearchIndexerService searchIndexerService,
    ILogger<DiscoveryCurationSubmitService> logger) : IDiscoveryCurationSubmitService
{
    public Task<DiscoveryCurationSubmitResult> SubmitAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken) =>
        IngestAsync(request, held: false, cancellationToken);

    public Task<DiscoveryCurationSubmitResult> ApplyHeldAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken) =>
        IngestAsync(request, held: true, cancellationToken);

    private async Task<DiscoveryCurationSubmitResult> IngestAsync(
        DiscoverySubmitRequest request,
        bool held,
        CancellationToken cancellationToken)
    {
        var claimed = false;
        var submitResults = new List<DiscoverySubmitItemOutcome>();
        try
        {

            // Claim every report before any ingestion so a concurrent hold (or another curator) cannot
            // also act on it. All-or-nothing; nothing is submitted when any claim fails.
            // Held reports (supervisor apply) are claimed PendingSupervisor -> Processed; others Unprocessed -> Processed.
            var unclaimed = held
                ? await discoveryResultsService.TryClaimHeldForProcessing(request.DiscoveryResultsDocumentIds,
                    cancellationToken)
                : await discoveryResultsService.TryClaimForProcessing(request.DiscoveryResultsDocumentIds,
                    cancellationToken);
            if (unclaimed.Count > 0)
            {
                logger.LogWarning("Refusing discovery submit: reports not claimable (held, processed or changed): {DocumentIds}.",
                    string.Join(",", unclaimed));
                return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Conflict,
                    UnavailableDocumentIds: unclaimed);
            }

            claimed = true;
            // Claimed: the client can no longer cancel; ingest and bookkeeping run to completion.
            var terminal = CancellationToken.None;

            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(request);
            var erroredResults = await IngestResults(discoveryResults, submitResults);

            await discoveryResultsService.MarkAsProcessed(request.DiscoveryResultsDocumentIds, request.ResultIds,
                erroredResults);

            var episodeIds = submitResults
                .Where(x => x.EpisodeId != null)
                .Select(x => x.EpisodeId!.Value);
            var indexed = await searchIndexerService.IndexEpisodes(episodeIds, terminal);

            await discoveryResultsService.UpdateDiscoveryInfoContent();

            var outcome = new DiscoverySubmitOutcome(
                "Success",
                erroredResults.Length > 0,
                submitResults,
                indexed);

            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok, outcome);
        }
        catch (Exception e)
        {
            if (claimed && submitResults.Count == 0)
            {
                // Nothing was ingested: hand the reports back so the submit can be retried.
                // Once any episode submit was attempted they stay Processed to avoid double ingestion.
                await ReleaseClaim(request.DiscoveryResultsDocumentIds, held);
            }

            logger.LogError(e, "Failure handling post of {nameofDiscoverySubmitRequest}",
                nameof(DiscoverySubmitRequest));
            return Failed(submitResults);
        }
    }

    public async Task<DiscoveryCurationSubmitResult> RetryItemsAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken)
    {
        var submitResults = new List<DiscoverySubmitItemOutcome>();
        try
        {
            // Retry acts on items of an already-applied submission: its reports must be Processed (never re-claimed).
            var documents = await discoveryResultsService.GetDocuments(request.DiscoveryResultsDocumentIds);
            var notProcessed = request.DiscoveryResultsDocumentIds
                .Where(id => documents.All(d => d.Id != id || d.State != DiscoveryResultsDocumentState.Processed))
                .ToArray();
            if (notProcessed.Length > 0)
            {
                return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Conflict,
                    UnavailableDocumentIds: notProcessed);
            }

            // Ingest has started: the client can no longer cancel it or the bookkeeping that follows.
            var terminal = CancellationToken.None;
            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(request);
            var erroredResults = await IngestResults(discoveryResults, submitResults);
            var succeeded = submitResults.Select(x => x.DiscoveryItemId).Except(erroredResults).ToArray();
            await discoveryResultsService.UpdateResultStates(request.DiscoveryResultsDocumentIds, succeeded,
                erroredResults);

            var indexed = await searchIndexerService.IndexEpisodes(
                submitResults.Where(x => x.EpisodeId != null).Select(x => x.EpisodeId!.Value), terminal);
            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
                new DiscoverySubmitOutcome("Success", erroredResults.Length > 0, submitResults, indexed));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure retrying discovery results.");
            return Failed(submitResults);
        }
    }

    private static DiscoveryCurationSubmitResult Failed(List<DiscoverySubmitItemOutcome> attempted) =>
        new(DiscoveryCurationSubmitStatus.Failed,
            attempted.Count == 0
                ? null
                : new DiscoverySubmitOutcome("Failure", true, attempted.ToArray(),
                    new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse()));

    /// <summary>Submits each result; per-item exceptions become <c>Error</c> outcomes carrying the reason.</summary>
    private async Task<Guid[]> IngestResults(IEnumerable<DiscoveryResult> discoveryResults,
        List<DiscoverySubmitItemOutcome> submitResults)
    {
        var indexingContext = new IndexingContext
        {
            SkipPodcastDiscovery = false,
            SkipExpensiveYouTubeQueries = false,
            SkipExpensiveSpotifyQueries = false
        };
        var submitOptions = new SubmitOptions(null, true, CreationSource: EpisodeCreationSource.Discovery);
        var erroredResults = new List<Guid>();
        foreach (var discoveryResult in discoveryResults)
        {
            logger.LogInformation(
                "Submitting discovery-result '{discoveryResultId}' with indexing-context: {indexingContext}",
                discoveryResult.Id, indexingContext);
            try
            {
                var result = await discoveryUrlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                submitResults.Add(new DiscoverySubmitItemOutcome(discoveryResult.Id, result.Episode?.Id,
                    result.Episode?.PodcastId, result.State.ToString()));
            }
            catch (Exception ex)
            {
                erroredResults.Add(discoveryResult.Id);
                submitResults.Add(new DiscoverySubmitItemOutcome(discoveryResult.Id, null, null,
                    DiscoverySubmitItemOutcomeMessages.Error, Describe(ex)));
                logger.LogError(ex, "{method} Failure submitting discovery-result '{discoveryResultId}'.",
                    nameof(IngestResults), discoveryResult.Id);
            }
        }

        return erroredResults.ToArray();
    }

    private static string Describe(Exception ex)
    {
        var text = $"{ex.GetType().Name}: {ex.Message}";
        return text.Length <= 500 ? text : text[..500];
    }

    private async Task ReleaseClaim(Guid[] documentIds, bool held)
    {
        try
        {
            // A supervisor apply hands held reports back to PendingSupervisor; a curator submit back to Unprocessed.
            if (held)
            {
                await discoveryResultsService.ReleaseHeldClaim(documentIds);
            }
            else
            {
                await discoveryResultsService.ReleaseClaim(documentIds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure releasing claimed discovery reports: {DocumentIds}.", string.Join(",", documentIds));
        }
    }
}
