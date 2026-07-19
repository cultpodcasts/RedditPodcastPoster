using System.Text.Json;
using Azure;
using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PushSubscriptions;

namespace Discovery;

[DurableTask(nameof(Discover))]
public class Discover(
    IOptions<DiscoverOptions> discoverOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    IDiscoveryLookbackResolver discoveryLookbackResolver,
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultsRepository discoveryResultsRepository,
    INotificationPublisher notificationPublisher,
    IDiscoveryInfoContentPublisher discoveryInfoContentPublisher,
    IActivityMarshaller activityMarshaller,
    ILogger<Discover> logger) : TaskActivity<DiscoveryContext, DiscoveryContext>
{
    private readonly DiscoverOptions _discoverOptions =
        discoverOptions.Value ??
        throw new ArgumentException($"Missing {nameof(DiscoverOptions)}.");
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<DiscoveryContext> RunAsync(TaskActivityContext context, DiscoveryContext input)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Discover));

        logger.LogInformation("{method}: discovery-options: {discoverOptions}",
            nameof(RunAsync), _discoverOptions);
        logger.LogInformation("{method}: discovery-context: {input}", nameof(RunAsync), input);

        DiscoveryLookbackResolution lookback;
        try
        {
            // Fail-closed: no watermark / Cosmos failure throws DiscoveryLookbackUnavailableException.
            lookback = await discoveryLookbackResolver.ResolveAsync();
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        var since = lookback.Since;
        logger.LogInformation(
            "Discovering items released since '{since:O}' (local:'{sinceLocal:O}', lookback:'Dynamic', latest-run:'{latestRun}'). ",
            since.ToUniversalTime(),
            since.ToLocalTime(),
            lookback.LatestSuccessfulDiscoveryBegan.ToString("O"));

        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        logger.LogInformation("{method}: {indexingContext}",
            nameof(RunAsync), indexingContext);

        if (DryRun.IsDiscoverDryRun)
        {
            memoryProbe.End(true);
            return input with
            {
                Success = true
            };
        }

        var activityBooked = await activityMarshaller.Initiate(input.DiscoveryOperationId, nameof(Discover));
        if (activityBooked != ActivityStatus.Initiated)
        {
            memoryProbe.End(true);
            return input with
            {
                DuplicateDiscoveryOperation = true
            };
        }

        try
        {
            var getServiceConfigOptions = new GetServiceConfigOptions(
                since, _discoverOptions.ExcludeSpotify, _discoverOptions.IncludeYouTube,
                _discoverOptions.IncludeListenNotes, _discoverOptions.IncludeTaddy, _discoverOptions.EnrichFromSpotify,
                _discoverOptions.EnrichFromApple, _discoverOptions.TaddyOffset);
            var discoveryConfig = discoveryConfigProvider.CreateDiscoveryConfig(getServiceConfigOptions);

            var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
            logger.LogInformation(
                "Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBeganLocal:O}'), indexing-context: {indexingContext}",
                discoveryBegan, discoveryBegan.ToLocalTime(), indexingContext);

            var preIndexingContextSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
            var discoveryResults = await discoveryService.GetDiscoveryResults(discoveryConfig, indexingContext).ToListAsync();
            var discoveryResultsDocument = new DiscoveryResultsDocument(discoveryBegan, discoveryResults)
            {
                SearchSince = (discoveryBegan - since).ToString()
            };
            EnrichDiscoveryResultsDocument(
                discoveryResultsDocument,
                _discoverOptions, indexingContext,
                preIndexingContextSkipSpotify);

            try
            {
                await discoveryResultsRepository.Save(discoveryResultsDocument);
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Failure to persist {DiscoveryResultsDocument}. Json: '{document}'",
                    nameof(DiscoveryResultsDocument), JsonSerializer.Serialize(discoveryResultsDocument));
                throw;
            }

            try
            {
                var discoveryInfo = await discoveryInfoContentPublisher.PublishUnprocessedSummaryAsync();

                if (discoveryInfo.DocumentCount > 0)
                {
                    await notificationPublisher.SendDiscoveryNotification(new DiscoveryNotification(
                        discoveryInfo.DocumentCount,
                        discoveryInfo.DiscoveryBegan ?? DateTime.MinValue,
                        discoveryInfo.NumberOfResults ?? 0));
                }
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Failure to persist {DiscoveryResultsDocument}. Failure to send-notification/publish-discovery-info.",
                    nameof(DiscoveryResultsDocument));
            }

            logger.LogWarning(
                "{method} complete. {nameofDiscoveryBegan}='{discoveryBegan:O}' document-id='{discoveryResultsDocumentId}' results-count='{discoveryResultsCount}' operation-id='{operationId}'.",
                nameof(RunAsync),
                nameof(discoveryBegan),
                discoveryBegan,
                discoveryResultsDocument.Id,
                discoveryResults.Count,
                input.DiscoveryOperationId);

            memoryProbe.End(true);

            return input with
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            logger.LogError(ex, "{method} did not complete.", nameof(RunAsync));
            throw new DiscoveryOrchestrationIncompleteException(
                $"Discover activity did not complete (operation-id='{input.DiscoveryOperationId}').",
                ex);
        }
        finally
        {
            try
            {
                activityBooked = await activityMarshaller.Complete(input.DiscoveryOperationId, nameof(Discover));
                if (activityBooked != ActivityStatus.Completed)
                {
                    logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to complete activity.");
            }
        }
    }

    private void EnrichDiscoveryResultsDocument(
        DiscoveryResultsDocument discoveryResultsDocument,
        DiscoverOptions discoverOptions,
        IndexingContext indexingContext,
        bool preIndexingContextSkipSpotify)
    {
        discoveryResultsDocument.ExcludeSpotify = discoverOptions.ExcludeSpotify;
        discoveryResultsDocument.IncludeYouTube = discoverOptions.IncludeYouTube;
        discoveryResultsDocument.IncludeListenNotes = discoverOptions.IncludeListenNotes;
        discoveryResultsDocument.IncludeTaddy = discoverOptions.IncludeTaddy;
        discoveryResultsDocument.EnrichFromSpotify = discoverOptions.EnrichFromSpotify;
        discoveryResultsDocument.EnrichFromApple = discoverOptions.EnrichFromApple;
        discoveryResultsDocument.PreSkipSpotifyUrlResolving = preIndexingContextSkipSpotify;
        discoveryResultsDocument.PostSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving;
    }
}