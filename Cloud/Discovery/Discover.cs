using System.Text.Json;
using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    ILogger<Discover> logger) : TaskActivity<DiscoveryContext, DiscoveryContext>
{
    private readonly DiscoverOptions _discoverOptions =
        discoverOptions.Value ??
        throw new ArgumentException($"Missing {nameof(DiscoverOptions)}.");

    public override async Task<DiscoveryContext> RunAsync(TaskActivityContext context, DiscoveryContext input)
    {
        logger.LogInformation($"{nameof(RunAsync)}: discovery-options: {_discoverOptions}");
        logger.LogInformation($"{nameof(RunAsync)}: discovery-context: {input}");
        var since = DateTime.UtcNow.Subtract(TimeSpan.Parse(_discoverOptions.SearchSince));
        logger.LogInformation(
            $"Discovering items released since '{since.ToUniversalTime():O}' (local:'{since.ToLocalTime():O}'). ");

        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        logger.LogInformation($"{nameof(RunAsync)}: {indexingContext}");

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
                $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}'), indexing-context: {indexingContext}");

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
                    $"Failure to persist {nameof(DiscoveryResultsDocument)}. Json: '{JsonSerializer.Serialize(discoveryResultsDocument)}'");
                throw;
            }

            try
            {
                var unprocessedDiscoveryReports = await discoveryResultsRepository.GetAllUnprocessed().ToListAsync();
                var numberOfReports = unprocessedDiscoveryReports.Count();
                var minProcessed = unprocessedDiscoveryReports.Min(x => x.DiscoveryBegan);
                var numberOfResults = unprocessedDiscoveryReports.SelectMany(x => x.DiscoveryResults).Count();
                await notificationPublisher.SendDiscoveryNotification(
                    new DiscoveryNotification(numberOfReports, minProcessed, numberOfResults));
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    $"Failure to persist {nameof(DiscoveryResultsDocument)}. Failure to send notification.");
            }

            logger.LogInformation(
                $"{nameof(RunAsync)} Complete. {nameof(discoveryBegan)}: '{discoveryBegan:O}', document-id: '{discoveryResultsDocument.Id}', results-count: '{discoveryResults.Count()}', indexing-context: {indexingContext}");
            results = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to execute {nameof(Discover)}.{nameof(RunAsync)}.");
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

        logger.LogInformation($"{nameof(RunAsync)} Completed");

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