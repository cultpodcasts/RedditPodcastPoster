using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discovery;

[DurableTask(nameof(Discover))]
public class Discover(
    IOptions<DiscoverOptions> discoverOptions,
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IActivityMarshaller activityMarshaller,
    ILogger<Discover> logger) : TaskActivity<DiscoveryContext, DiscoveryContext>
{
    private readonly DiscoverOptions _discoverOptions = discoverOptions.Value;

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
            var serviceConfigs = discoveryConfigProvider.GetServiceConfigs(_discoverOptions.ExcludeSpotify,
                _discoverOptions.IncludeYouTube, _discoverOptions.IncludeListenNotes);

            var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
            logger.LogInformation(
                $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}'), indexing-context: {indexingContext}");
            var discoveryConfig = new DiscoveryConfig(serviceConfigs, _discoverOptions.EnrichListenNotesFromSpotify,
                _discoverOptions.EnrichFromApple);

            var preIndexingContextSkipSpotify = indexingContext.SkipSpotifyUrlResolving;
            var discoveryResults = await discoveryService.GetDiscoveryResults(indexingContext, discoveryConfig);
            var discoveryResultsDocument = new DiscoveryResultsDocument(discoveryBegan, discoveryResults)
            {
                SearchSince = _discoverOptions.SearchSince
            };
            EnrichDiscoveryResultsDocument(
                discoveryResultsDocument,
                _discoverOptions, indexingContext,
                preIndexingContextSkipSpotify);

            await discoveryResultsRepository.Save(discoveryResultsDocument);

            logger.LogInformation(
                $"{nameof(RunAsync)} Complete. {nameof(discoveryBegan)}: '{discoveryBegan:O}', document-id: '{discoveryResultsDocument.Id}', no-results: '{discoveryResults.Count()}', indexing-context: {indexingContext}.");
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
        discoveryResultsDocument.EnrichListenNotesFromSpotify = discoverOptions.EnrichListenNotesFromSpotify;
        discoveryResultsDocument.PreSkipSpotifyUrlResolving = preIndexingContextSkipSpotify;
        discoveryResultsDocument.PostSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving;
    }
}