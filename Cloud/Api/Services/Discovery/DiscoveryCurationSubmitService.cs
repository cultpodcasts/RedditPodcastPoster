using Api.Models;
using Api.Services.SubmitUrl;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Languages;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Languages;
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
    ILookupRepository lookupRepository,
    IDiscoveryKindSuggester discoveryKindSuggester,
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
        var hadResultIds = request.ResultIds.Length > 0;
        request = WithoutCancelled(request);
        if (hadResultIds && request.ResultIds.Length == 0)
        {
            return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
                new DiscoverySubmitOutcome("Success", false, [], new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse()));
        }

        var supported = await SupportedLanguageCodes();
        DiscoveryCurationSubmitResult? preflight;
        try
        {
            preflight = await PreflightLanguages(request, supported);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Language probe failed before discovery reports were claimed.");
            return Failed([]);
        }

        if (preflight != null)
        {
            return preflight;
        }

        var claimed = false;
        var submitResults = new List<DiscoverySubmitItemOutcome>();
        try
        {

            // Claim every report before any persisting ingestion so a concurrent hold (or another curator) cannot
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
            var erroredResults = await IngestResults(discoveryResults, submitResults, rejectedRemoved, request, supported);
            var accepted = request.ResultIds.Except(erroredResults).Except(rejectedRemoved).ToArray();

            await discoveryResultsService.MarkAsProcessed(request.DiscoveryResultsDocumentIds, accepted,
                erroredResults, submitResults);

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

            var hadResultIds = request.ResultIds.Length > 0;
            request = WithoutCancelled(request);
            if (hadResultIds && request.ResultIds.Length == 0)
            {
                return new DiscoveryCurationSubmitResult(DiscoveryCurationSubmitStatus.Ok,
                    new DiscoverySubmitOutcome("Success", false, [],
                        new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse()));
            }

            var supported = await SupportedLanguageCodes();
            var preflight = await PreflightLanguages(request, supported);
            if (preflight != null)
            {
                return preflight;
            }

            // Ingest has started: the client can no longer cancel it or the bookkeeping that follows.
            var terminal = CancellationToken.None;
            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(request);
            var rejectedRemoved = new List<Guid>();
            var erroredResults = await IngestResults(discoveryResults, submitResults, rejectedRemoved, request, supported);
            var succeeded = submitResults.Select(x => x.DiscoveryItemId).Except(erroredResults)
                .Except(rejectedRemoved).ToArray();
            await discoveryResultsService.UpdateResultStates(request.DiscoveryResultsDocumentIds, succeeded,
                erroredResults, submitResults);

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
        List<DiscoverySubmitItemOutcome> submitResults, List<Guid> rejectedRemoved,
        DiscoverySubmitRequest request, IReadOnlyCollection<string> supportedLanguageCodes)
    {
        var indexingContext = new IndexingContext
        {
            SkipPodcastDiscovery = false,
            SkipExpensiveYouTubeQueries = false,
            SkipExpensiveSpotifyQueries = false
        };
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
                var submitOptions = CreateOptions(request, discoveryResult, supportedLanguageCodes, probe: false);
                var result = await discoveryUrlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                if (result.LanguageHold is { Code: { Length: > 0 } })
                {
                    erroredResults.Add(discoveryResult.Id);
                    submitResults.Add(new DiscoverySubmitItemOutcome(discoveryResult.Id, null, null,
                        "LanguageDecisionRequired"));
                    continue;
                }

                if (result.IdentityHold is { Candidates.Count: > 0 })
                {
                    erroredResults.Add(discoveryResult.Id);
                    submitResults.Add(new DiscoverySubmitItemOutcome(discoveryResult.Id, null, null,
                        "IdentityDecisionRequired"));
                    continue;
                }

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

                if (result.FilmSearchSource is { } film)
                {
                    // A film is not in the episodes container either: index its document directly.
                    await CatalogueSubmitSearchIndexing.IndexFilmAsync(playableSearch, film, logger,
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
    /// Reads each item and detects language, kind suggestions, and identity without persisting.
    /// Language is asked first, then a suggest-only kind hint (#1200), then identity.
    /// Nothing is claimed or created while a gate is outstanding.
    /// </summary>
    private async Task<DiscoveryCurationSubmitResult?> PreflightLanguages(
        DiscoverySubmitRequest request,
        IReadOnlyCollection<string> supportedLanguageCodes)
    {
        var pendingResults = discoveryResultsService.GetDiscoveryResult(request);
        var discoveryResults = pendingResults is null ? [] : await pendingResults ?? [];
        var holds = new List<CatalogueLanguageHold>();
        // Language / identity probe only — skip expensive Spotify/YouTube walks so preflight does not
        // double the wall-clock of the real ingest (and blow edge timeouts before the 200 is sent).
        var indexingContext = new IndexingContext
        {
            SkipPodcastDiscovery = false,
            SkipExpensiveYouTubeQueries = true,
            SkipExpensiveSpotifyQueries = true
        };
        foreach (var discoveryResult in discoveryResults)
        {
            if (await IsStoredAgainstRemovedPodcast(discoveryResult))
            {
                continue;
            }

            try
            {
                var probed = await discoveryUrlSubmitter.Submit(
                    discoveryResult,
                    indexingContext,
                    CreateOptions(request, discoveryResult, supportedLanguageCodes, probe: true));
                if (probed?.LanguageHold is { Code: { Length: > 0 } } hold)
                {
                    holds.Add(hold with { DiscoveryItemId = discoveryResult.Id });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Language probe failed for discovery-result '{DiscoveryResultId}'; the create call records the error.",
                    discoveryResult.Id);
            }
        }

        if (holds.Count > 0)
        {
            return new DiscoveryCurationSubmitResult(
                DiscoveryCurationSubmitStatus.LanguageDecisionRequired,
                new DiscoverySubmitOutcome(
                    "LanguageDecisionRequired",
                    false,
                    [],
                    new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse(),
                    holds));
        }

        var kindHolds = new List<DiscoveryKindHold>();
        foreach (var discoveryResult in discoveryResults)
        {
            if (await IsStoredAgainstRemovedPodcast(discoveryResult))
            {
                continue;
            }

            if (KindChoiceFor(request, discoveryResult.Id) is DiscoveryKindChoice.Select or DiscoveryKindChoice.Continue)
            {
                continue;
            }

            try
            {
                var suggestion = await discoveryKindSuggester.SuggestAsync(discoveryResult);
                if (suggestion is { Candidates.Count: > 0 })
                {
                    kindHolds.Add(suggestion with { DiscoveryItemId = discoveryResult.Id });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Kind suggestion failed for discovery-result '{DiscoveryResultId}'; continuing without a hint.",
                    discoveryResult.Id);
            }
        }

        if (kindHolds.Count > 0)
        {
            return new DiscoveryCurationSubmitResult(
                DiscoveryCurationSubmitStatus.KindDecisionRequired,
                new DiscoverySubmitOutcome(
                    "KindDecisionRequired",
                    false,
                    [],
                    new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse(),
                    KindDecisionsRequired: kindHolds));
        }

        var identityHolds = new List<CatalogueIdentityHold>();
        foreach (var discoveryResult in discoveryResults)
        {
            if (await IsStoredAgainstRemovedPodcast(discoveryResult))
            {
                continue;
            }

            try
            {
                var probed = await discoveryUrlSubmitter.Submit(
                    discoveryResult,
                    indexingContext,
                    CreateOptions(request, discoveryResult, supportedLanguageCodes, probe: true));
                if (probed?.IdentityHold is { Candidates.Count: > 0 } identityHold)
                {
                    identityHolds.Add(identityHold with { DiscoveryItemId = discoveryResult.Id });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Identity probe failed for discovery-result '{DiscoveryResultId}'; the create call records the error.",
                    discoveryResult.Id);
            }
        }

        if (identityHolds.Count > 0)
        {
            return new DiscoveryCurationSubmitResult(
                DiscoveryCurationSubmitStatus.IdentityDecisionRequired,
                new DiscoverySubmitOutcome(
                    "IdentityDecisionRequired",
                    false,
                    [],
                    new RedditPodcastPoster.EntitySearchIndexer.Models.EntitySearchIndexerResponse(),
                    IdentityDecisionsRequired: identityHolds));
        }

        return null;
    }

    private static DiscoveryKindChoice KindChoiceFor(DiscoverySubmitRequest request, Guid resultId)
    {
        if (request.KindDecisions != null &&
            request.KindDecisions.TryGetValue(resultId, out var raw))
        {
            return DiscoveryKindChoices.Parse(raw);
        }

        return DiscoveryKindChoice.Unset;
    }

    private static DiscoverySubmitRequest WithoutCancelled(DiscoverySubmitRequest request)
    {
        var cancelled = new HashSet<Guid>();
        if (request.LanguageDecisions is { Count: > 0 })
        {
            foreach (var pair in request.LanguageDecisions
                         .Where(x => CatalogueLanguageChoices.Parse(x.Value) == CatalogueLanguageChoice.Cancel))
            {
                cancelled.Add(pair.Key);
            }
        }

        if (request.IdentityDecisions is { Count: > 0 })
        {
            foreach (var pair in request.IdentityDecisions
                         .Where(x => CatalogueIdentityChoices.Parse(x.Value) == CatalogueIdentityChoice.Cancel))
            {
                cancelled.Add(pair.Key);
            }
        }

        if (request.KindDecisions is { Count: > 0 })
        {
            foreach (var pair in request.KindDecisions
                         .Where(x => DiscoveryKindChoices.Parse(x.Value) == DiscoveryKindChoice.Cancel))
            {
                cancelled.Add(pair.Key);
            }
        }

        if (cancelled.Count == 0)
        {
            return request;
        }

        return new DiscoverySubmitRequest
        {
            DiscoveryResultsDocumentIds = request.DiscoveryResultsDocumentIds,
            ResultIds = request.ResultIds.Where(id => !cancelled.Contains(id)).ToArray(),
            LanguageDecisions = request.LanguageDecisions,
            IdentityDecisions = request.IdentityDecisions,
            SelectedIdentityKeys = request.SelectedIdentityKeys,
            KindDecisions = request.KindDecisions,
            SelectedKindKeys = request.SelectedKindKeys
        };
    }

    private static SubmitOptions CreateOptions(
        DiscoverySubmitRequest request,
        DiscoveryResult discoveryResult,
        IReadOnlyCollection<string> supportedLanguageCodes,
        bool probe)
    {
        var resultId = discoveryResult.Id;
        var choice = CatalogueLanguageChoice.Unset;
        if (request.LanguageDecisions != null &&
            request.LanguageDecisions.TryGetValue(resultId, out var raw))
        {
            choice = CatalogueLanguageChoices.Parse(raw);
        }

        var identityChoice = CatalogueIdentityChoice.Unset;
        string? selectedKey = null;
        if (request.IdentityDecisions != null &&
            request.IdentityDecisions.TryGetValue(resultId, out var identityRaw))
        {
            identityChoice = CatalogueIdentityChoices.Parse(identityRaw);
        }

        if (request.SelectedIdentityKeys != null &&
            request.SelectedIdentityKeys.TryGetValue(resultId, out var key))
        {
            selectedKey = key;
        }

        var kindChoice = KindChoiceFor(request, resultId);
        string? selectedKindKey = null;
        if (request.SelectedKindKeys != null &&
            request.SelectedKindKeys.TryGetValue(resultId, out var kindKey))
        {
            selectedKindKey = kindKey;
        }

        DiscoveryKindApplication? kindApplication = null;
        if (kindChoice == DiscoveryKindChoice.Select)
        {
            var url = discoveryResult.Urls.Spotify ?? discoveryResult.Urls.Apple ?? discoveryResult.Urls.YouTube;
            if (url != null)
            {
                kindApplication = SubmitContentClassifier.FromDiscovery(url, selectedKindKey);
            }
        }

        if (kindApplication?.SelectedIdentityKey is { Length: > 0 } kindIdentityKey &&
            identityChoice == CatalogueIdentityChoice.Unset)
        {
            identityChoice = kindApplication.IdentityChoice;
            selectedKey = kindIdentityKey;
        }

        return new SubmitOptions(
            null,
            true,
            PersistToDatabase: !probe,
            CreationSource: EpisodeCreationSource.Discovery,
            ClassificationSignals: kindApplication?.Signals,
            Parent: kindApplication?.Parent,
            EnforceLanguageGate: true,
            SupportedLanguageCodes: supportedLanguageCodes,
            LanguageChoice: choice,
            LanguageProbe: probe,
            IdentityChoice: identityChoice,
            SelectedIdentityKey: selectedKey,
            KindChoice: kindChoice,
            SelectedKindKey: selectedKindKey,
            TargetFilmId: kindApplication?.TargetFilmId,
            TargetNewsOrganisationId: kindApplication?.TargetNewsOrganisationId,
            TargetNewsReportId: kindApplication?.TargetNewsReportId);
    }

    private async Task<IReadOnlyCollection<string>> SupportedLanguageCodes()
    {
        var pending = lookupRepository.GetSupportedLanguagesConfig();
        var config = pending == null ? null : await pending;
        config ??= SupportedLanguagesConfig.CreateDefault();
        return config.Languages.Select(x => x.Code).ToArray();
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
