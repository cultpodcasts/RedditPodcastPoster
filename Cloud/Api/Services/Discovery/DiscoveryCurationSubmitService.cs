using Api.Models;
using Api.Services.SubmitUrl;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Services.Discovery;

public class DiscoveryCurationSubmitService(
    IDiscoveryResultsService discoveryResultsService,
    IDiscoveryUrlSubmitter discoveryUrlSubmitter,
    IEpisodeSearchIndexerService searchIndexerService,
    IPlayableSearchDocumentSwap playableSearch,
    IPodcastRepository podcastRepository,
    IPodcastService podcastService,
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
            var rejectedRemoved = new List<Guid>();
            var erroredResults = await IngestResults(discoveryResults, submitResults, rejectedRemoved);
            var accepted = request.ResultIds.Except(erroredResults).Except(rejectedRemoved).ToArray();

            await discoveryResultsService.MarkAsProcessed(request.DiscoveryResultsDocumentIds, accepted,
                erroredResults);

            var indexed = await searchIndexerService.IndexEpisodes(PodcastEpisodeIds(submitResults), terminal);

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
            var rejectedRemoved = new List<Guid>();
            var erroredResults = await IngestResults(discoveryResults, submitResults, rejectedRemoved);
            var succeeded = submitResults.Select(x => x.DiscoveryItemId).Except(erroredResults)
                .Except(rejectedRemoved).ToArray();
            await discoveryResultsService.UpdateResultStates(request.DiscoveryResultsDocumentIds, succeeded,
                erroredResults);

            var indexed = await searchIndexerService.IndexEpisodes(PodcastEpisodeIds(submitResults), terminal);
            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
                new DiscoverySubmitOutcome("Success", erroredResults.Length > 0, submitResults, indexed));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failure retrying discovery results.");
            return Failed(submitResults);
        }
    }

    /// <summary>Podcast episodes only: news reports / TV episodes (with a content kind) are indexed at submit.</summary>
    private static IEnumerable<Guid> PodcastEpisodeIds(IEnumerable<DiscoverySubmitItemOutcome> outcomes) =>
        outcomes.Where(x => x.EpisodeId != null && x.ContentKind == null).Select(x => x.EpisodeId!.Value);

    private static DiscoveryCurationSubmitResult Failed(List<DiscoverySubmitItemOutcome> attempted) =>
        new(DiscoveryCurationSubmitStatus.Failed,
            attempted.Count == 0
                ? null
                : new DiscoverySubmitOutcome("Failure", true, attempted.ToArray(),
                    new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse()));

    /// <summary>Submits each result; per-item exceptions become <c>Error</c> outcomes carrying the reason.</summary>
    private async Task<Guid[]> IngestResults(IEnumerable<DiscoveryResult> discoveryResults,
        List<DiscoverySubmitItemOutcome> submitResults, List<Guid> rejectedRemoved)
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
            if (await IsStoredAgainstRemovedPodcast(discoveryResult))
            {
                rejectedRemoved.Add(discoveryResult.Id);
                logger.LogInformation(
                    "Rejecting discovery-result '{DiscoveryResultId}' because its podcast is removed.",
                    discoveryResult.Id);
                continue;
            }

            logger.LogInformation(
                "Submitting discovery-result '{discoveryResultId}' with indexing-context: {indexingContext}",
                discoveryResult.Id, indexingContext);
            try
            {
                var result = await discoveryUrlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                submitResults.Add(new DiscoverySubmitItemOutcome(discoveryResult.Id,
                    result.Episode?.Id ?? result.PlayableId,
                    result.Episode?.PodcastId ?? result.ParentId,
                    result.State.ToString(),
                    ContentKind: result.ContentKind));
                if (result.CatalogueSearchSource is { } searchSource)
                {
                    // A news report / TV episode is not in the episodes container: index its document directly.
                    await CatalogueSubmitSearchIndexing.IndexAsync(playableSearch, searchSource, logger,
                        CancellationToken.None);
                }
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

    /// <summary>
    /// True when the podcast that owns this stored result is removed. A live playlist-scoped show counts only when
    /// the video is in its playlist, so a removed channel-wide outlet still rejects its own episodes. URL resolution
    /// of a missing video stays on the ban command; save only refuses what the queue already records.
    /// </summary>
    private async Task<bool> IsStoredAgainstRemovedPodcast(DiscoveryResult result)
    {
        var candidates = new List<Podcast>();
        if (result.MatchingPodcastIds is { Length: > 0 } ids)
        {
            candidates.AddRange(await podcastRepository.GetAllBy(x => Enumerable.Contains(ids, x.Id)).ToListAsync());
        }

        var channel = result.YouTubeChannelId;
        if (!string.IsNullOrWhiteSpace(channel))
        {
            candidates.AddRange(await podcastRepository.GetAllBy(x => x.YouTubeChannelId == channel).ToListAsync());
        }

        var spotify = result.SpotifyId;
        if (!string.IsNullOrWhiteSpace(spotify))
        {
            candidates.AddRange(await podcastRepository.GetAllBy(x => x.SpotifyId == spotify).ToListAsync());
        }

        if (result.AppleId is { } apple)
        {
            candidates.AddRange(await podcastRepository.GetAllBy(x => x.AppleId == apple).ToListAsync());
        }

        var owners = new List<Podcast>();
        foreach (var podcast in candidates.DistinctBy(x => x.Id))
        {
            if (string.IsNullOrEmpty(podcast.YouTubePlaylistId) || podcast.IsRemoved() ||
                await VideoIsInPlaylist(result, podcast))
            {
                owners.Add(podcast);
            }
        }

        return PodcastShowOwnership.RemovedShowSuppresses(owners);
    }

    private async Task<bool> VideoIsInPlaylist(DiscoveryResult result, Podcast podcast)
    {
        if (result.Urls.YouTube is not { } url)
        {
            return false;
        }

        return await podcastService.IsVideoInPodcastPlaylist(podcast, url, new IndexingContext()) == true;
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
