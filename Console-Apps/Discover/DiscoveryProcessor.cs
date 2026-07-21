using System.Globalization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.ML;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Discovery.Providers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Discover;

public class DiscoveryProcessor(
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultScorer discoveryResultScorer,
    IDiscoveryResultConsoleLogger discoveryResultConsoleLogger,
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryInfoContentPublisher discoveryInfoContentPublisher,
    ILogger<DiscoveryProcessor> logger
)
{
    public async Task<DiscoveryResponse> Process(DiscoveryRequest request)
    {
        var fg = Console.ForegroundColor;

        IList<DiscoveryResult> discoveryResults;
        DateTime? latest;
        List<Guid>? unprocessedEpisodeIds = null;
        if (request.UseRemote)
        {
            unprocessedEpisodeIds = [];
            discoveryResults = await GetRemoteDiscoveryResults(unprocessedEpisodeIds);
            latest = discoveryResults.LastOrDefault()?.Released;
        }
        else
        {
            if (discoveryResultScorer.IsEnabled)
            {
                logger.LogInformation("Discovery ML scorer enabled; results will be ranked by accept probability.");
            }
            else
            {
                logger.LogWarning(
                    "Discovery ML scorer is not enabled or model files could not be loaded. " +
                    "Results will not be ranked. Check discover:scorer in appsettings and az login for blob access.");
            }

            var discoveryContext = CreateDiscoveryContext(request);
            discoveryResults = OrderDiscoveryResults(
                await GetDiscoveryResults(request, discoveryContext.Since));
            await SaveDiscoveryResults(request, discoveryContext, discoveryResults);
            await PublishDiscoveryInfo();
            latest = discoveryContext.DiscoveryBegan;
        }

        foreach (var episode in discoveryResults)
        {
            discoveryResultConsoleLogger.DisplayEpisode(episode, fg);
        }

        if (request.UseRemote)
        {
            logger.LogInformation(
                "{method}: marking {documentType} ids as processed: {documentIds}.",
                nameof(Process), nameof(DiscoveryResultsDocument), string.Join(", ", unprocessedEpisodeIds!));
            await discoveryResultsRepository.SetProcessed(unprocessedEpisodeIds!);
        }

        return new DiscoveryResponse(latest);
    }

    private (DateTime Since, string SearchSince, DateTime DiscoveryBegan) CreateDiscoveryContext(DiscoveryRequest request)
    {
        DateTime since;
        string searchSince;
        if (request.Since.HasValue)
        {
            if (request.Since.Value.ToUniversalTime() > DateTime.UtcNow)
            {
                throw new InvalidOperationException(
                    $"'{nameof(request)}.{nameof(request.Since)}' is in the future. ");
            }

            since = request.Since.Value.ToUniversalTime();
            searchSince = DateTime.UtcNow.Subtract(since).ToString();
        }
        else if (!string.IsNullOrWhiteSpace(request.SearchWindow))
        {
            var window = ParseSearchWindow(request.SearchWindow);
            since = DateTime.UtcNow.Subtract(window);
            searchSince = window.ToString();
        }
        else
        {
            throw new InvalidOperationException("Unable to determine baseline-time to discover from.");
        }

        Console.WriteLine(
            $"Discovering items released since '{since.ToUniversalTime():O}' (local:'{since.ToLocalTime():O}').");

        var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
        Console.WriteLine(
            $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}').");

        return (since, searchSince, discoveryBegan);
    }

    private async Task<List<DiscoveryResult>> GetDiscoveryResults(DiscoveryRequest request, DateTime since)
    {
        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var discoveryConfig = discoveryConfigProvider.CreateDiscoveryConfig(
            new GetServiceConfigOptions(since, request.ExcludeSpotify, request.IncludeYouTube,
                request.IncludeListenNotes, request.IncludeTaddy, request.EnrichFromSpotify,
                request.EnrichFromApple, request.GetTaddyOffset()));

        return await discoveryService
            .GetDiscoveryResults(discoveryConfig, indexingContext)
            .ToListAsync();
    }

    private async Task SaveDiscoveryResults(
        DiscoveryRequest request,
        (DateTime Since, string SearchSince, DateTime DiscoveryBegan) discoveryContext,
        IList<DiscoveryResult> discoveryResults)
    {
        var indexingContext = new IndexingContext(
            discoveryContext.Since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var discoveryResultsDocument = new DiscoveryResultsDocument(discoveryContext.DiscoveryBegan, discoveryResults)
        {
            SearchSince = discoveryContext.SearchSince,
            ExcludeSpotify = request.ExcludeSpotify,
            IncludeYouTube = request.IncludeYouTube,
            IncludeListenNotes = request.IncludeListenNotes,
            IncludeTaddy = request.IncludeTaddy,
            EnrichFromSpotify = request.EnrichFromSpotify,
            EnrichFromApple = request.EnrichFromApple,
            PreSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving,
            PostSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving
        };

        await discoveryResultsRepository.Save(discoveryResultsDocument);
        logger.LogInformation(
            "{method}: persisted {documentType} with id '{documentId}'.",
            nameof(Process), nameof(DiscoveryResultsDocument), discoveryResultsDocument.Id);
    }

    private async Task PublishDiscoveryInfo()
    {
        try
        {
            var discoveryInfo = await discoveryInfoContentPublisher.PublishUnprocessedSummaryAsync();
            logger.LogInformation(
                "{method}: published discovery-info for {documentCount} document(s) and {visibleResultCount} visible deduped result(s).",
                nameof(PublishDiscoveryInfo),
                discoveryInfo.DocumentCount,
                discoveryInfo.NumberOfResults ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: failure to publish discovery-info.", nameof(PublishDiscoveryInfo));
        }
    }

    private async Task<List<DiscoveryResult>> GetRemoteDiscoveryResults(List<Guid> unprocessedEpisodeIds)
    {
        var remoteDiscoveryResults = new List<DiscoveryResult>();
        await foreach (var report in discoveryResultsRepository.GetAllUnprocessed())
        {
            unprocessedEpisodeIds.Add(report.Id);
            remoteDiscoveryResults.AddRange(report.DiscoveryResults);
        }

        return remoteDiscoveryResults.OrderBy(x => x.Released).ToList();
    }

    private static List<DiscoveryResult> OrderDiscoveryResults(IList<DiscoveryResult> discoveryResults) =>
        discoveryResults
            .OrderByDescending(x => x.AcceptProbability ?? -1f)
            .ThenBy(x => x.Released)
            .ToList();

    private static TimeSpan ParseSearchWindow(string value) =>
        value.Contains(':')
            ? TimeSpan.Parse(value, CultureInfo.InvariantCulture)
            : TimeSpan.FromHours(int.Parse(value, CultureInfo.InvariantCulture));
}
