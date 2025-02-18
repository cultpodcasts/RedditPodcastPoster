using System.Text.Json;
using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PushSubscriptions;

namespace Discovery;

[DurableTask(nameof(Discover))]
public class Discover(
    IOptions<DiscoverOptions> discoverOptions,
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultsRepository discoveryResultsRepository,
    INotificationPublisher notificationPublisher,
    IActivityMarshaller activityMarshaller,
    IContentPublisher contentPublisher,
    ILogger<Discover> logger) : TaskActivity<DiscoveryContext, DiscoveryContext>
{
    private readonly DiscoverOptions _discoverOptions =
        discoverOptions.Value ??
        throw new ArgumentException($"Missing {nameof(DiscoverOptions)}.");

    public override async Task<DiscoveryContext> RunAsync(TaskActivityContext context, DiscoveryContext input)
    {
        logger.LogInformation("{method}: discovery-options: {discoverOptions}",
            nameof(RunAsync), _discoverOptions);
        logger.LogInformation("{method}: discovery-context: {input}", nameof(RunAsync), input);
        var since = DateTime.UtcNow.Subtract(TimeSpan.Parse(_discoverOptions.SearchSince));
        logger.LogInformation(
            "Discovering items released since '{since:O}' (local:'{sinceLocal:O}'). ",
            since.ToUniversalTime(), since.ToLocalTime());

        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        logger.LogInformation("{method}: {indexingContext}",
            nameof(RunAsync), indexingContext);

        if (DryRun.IsDiscoverDryRun)
        {
            return input with
            {
                Success = true
            };
        }

        var activityBooked = await activityMarshaller.Initiate(input.DiscoveryOperationId, nameof(Discover));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return input with
            {
                DuplicateDiscoveryOperation = true
            };
        }

        bool results;
        try
        {
            var getServiceConfigOptions = new GetServiceConfigOptions(
                since, _discoverOptions.ExcludeSpotify, _discoverOptions.IncludeYouTube,
                _discoverOptions.IncludeListenNotes, _discoverOptions.IncludeTaddy, _discoverOptions.EnrichFromSpotify,
                _discoverOptions.EnrichFromApple, _discoverOptions.TaddyOffset);
            var discoveryConfig = discoveryConfigProvider.CreateDiscoveryConfig(getServiceConfigOptions);

            var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
            logger.LogInformation(
                "Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBeganLocal():O}'), indexing-context: {indexingContext}",
                discoveryBegan, discoveryBegan.ToLocalTime(), indexingContext);

            var preIndexingContextSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
            var discoveryResults = await discoveryService.GetDiscoveryResults(discoveryConfig, indexingContext);
            var discoveryResultsDocument = new DiscoveryResultsDocument(discoveryBegan, discoveryResults)
            {
                SearchSince = _discoverOptions.SearchSince
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
                var unprocessedDiscoveryReports = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync();
                var numberOfReports = unprocessedDiscoveryReports.Count();
                DateTime? minProcessed = null;
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

                if (numberOfReports > 0)
                {
                    await notificationPublisher.SendDiscoveryNotification(new DiscoveryNotification(numberOfReports,
                        minProcessed ?? DateTime.MinValue, numberOfResults ?? 0));
                }
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Failure to persist {DiscoveryResultsDocument}. Failure to send-notification/publish-discovery-info.",
                    nameof(DiscoveryResultsDocument));
            }

            logger.LogInformation(
                "{method} Complete. {nameofDiscoveryBegan}: '{discoveryBegan:O}', document-id: '{discoveryResultsDocumentId}', results-count: '{discoveryResultsCount}', indexing-context: {indexingContext}",
                nameof(RunAsync), nameof(discoveryBegan), discoveryBegan, discoveryResultsDocument.Id,
                discoveryResults.Count(), indexingContext);
            results = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {nameofDiscover}.{method}.",
                nameof(Discover), nameof(RunAsync));
            results = false;
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

        if (!results)
        {
            logger.LogError("Failure occurred");
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));

        return input with
        {
            Success = results
        };
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